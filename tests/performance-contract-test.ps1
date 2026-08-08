$ErrorActionPreference = "Stop"

$pluginRoot = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
$pluginSource = Get-Content -LiteralPath (Join-Path $pluginRoot "Plugin.cs") -Raw
$collectorSource = Get-Content -LiteralPath (Join-Path $pluginRoot "DirectGameSnapshotCollector.cs") -Raw

function Assert-Contains([string]$Text, [string]$Needle, [string]$Message) {
  if ($Text.IndexOf($Needle, [StringComparison]::Ordinal) -lt 0) { throw $Message }
}

function Assert-NotContains([string]$Text, [string]$Needle, [string]$Message) {
  if ($Text.IndexOf($Needle, [StringComparison]::Ordinal) -ge 0) { throw $Message }
}

$collectIndex = $pluginSource.IndexOf("DirectGameSnapshotCollector.Collect", [StringComparison]::Ordinal)
$workerIndex = $pluginSource.IndexOf("Task.Run(() => snapshots.Select(snapshot => PrepareSnapshot", [StringComparison]::Ordinal)
if ($collectIndex -lt 0 -or $workerIndex -lt 0 -or $collectIndex -ge $workerIndex) {
  throw "Native collection must finish before managed payload preparation moves off-thread."
}
Assert-Contains $pluginSource "await framework.RunOnFrameworkThread" "All game-owned state capture must be marshaled to the framework thread."

Assert-Contains $pluginSource "JsonSerializer.SerializeToUtf8Bytes(snapshot.Payload)" "Snapshots must be serialized once into UTF-8 bytes."
Assert-Contains $pluginSource "writer.WriteRawValue(payloadUtf8, skipInputValidation: true)" "The prepared payload must be reused verbatim inside the upload wrapper."
Assert-NotContains $pluginSource "GetPayloadHash(object payload)" "The legacy second serialization path must remain removed."
Assert-NotContains $collectorSource "JsonSerializer.Serialize(prior) == JsonSerializer.Serialize(read)" "Retainer listing comparisons must not serialize both snapshots."
Assert-Contains $collectorSource "prior.Items.SequenceEqual(read.Items)" "Retainer listing changes must use typed structural comparison."
Assert-Contains $collectorSource 'JsonPropertyName("retainerId")' "Typed retainer rows must preserve the existing camel-case wire contract."
Assert-Contains $collectorSource "SheetRowCache<T>.Get(dataManager)" "Static Lumina row catalogs must be cached."

$syncStart = $pluginSource.IndexOf("private async Task SyncAsync", [StringComparison]::Ordinal)
$syncEnd = $pluginSource.IndexOf("private void DrawSettings", $syncStart, [StringComparison]::Ordinal)
if ($syncStart -lt 0 -or $syncEnd -le $syncStart) { throw "Unable to inspect SyncAsync." }
$syncBody = $pluginSource.Substring($syncStart, $syncEnd - $syncStart)
$syncWorkerIndex = $syncBody.IndexOf("Task.Run(() => snapshots.Select(snapshot => PrepareSnapshot", [StringComparison]::Ordinal)
$afterWorker = $syncBody.Substring($syncWorkerIndex)
Assert-NotContains $afterWorker "objects.LocalPlayer" "Worker continuations must use the captured character identity, not Dalamud object state."
Assert-Contains $pluginSource "AutomaticFailureRetrySeconds" "Automatic failures must use bounded retry backoff instead of retrying every frame."
$saveCount = ([regex]::Matches($syncBody, "SaveConfigurationAsync\(\)")).Count
if ($saveCount -ne 1) { throw "SyncAsync must batch successful sync-state persistence into exactly one save; found $saveCount." }

Assert-Contains $pluginSource "Start 10-minute diagnostic recording" "Public diagnostics must be explicitly started by the user."
Assert-Contains $pluginSource "if (!IsDiagnosticRecording) return;" "Public diagnostics must remain idle by default."
Assert-Contains $pluginSource "if (diagnostics.Count > 40)" "Diagnostic history must remain bounded."
Assert-Contains $pluginSource "It never uploads logs, chat text, credentials, or device identifiers." "The public UI must state the diagnostic privacy boundary."

Write-Output "Gillions Game Sync performance contract checks passed."
