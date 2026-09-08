$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$plugin = Get-Content -LiteralPath (Join-Path $root 'Plugin.cs') -Raw
$runtime = $plugin.Substring(0, $plugin.IndexOf('[Newtonsoft.Json.JsonConverter'))
$collector = Get-Content -LiteralPath (Join-Path $root 'DirectGameSnapshotCollector.cs') -Raw
$retainers = Get-Content -LiteralPath (Join-Path $root 'RetainerVentureSnapshots.cs') -Raw
function Require([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
Require (-not [regex]::IsMatch($runtime, 'configuration\.(PendingGilLedgerEvents|PendingRetainerSales|PendingRetainerGilReceipts|PendingRetainerGilDeposits|RetainerVentureStates|RetainerGilBalances|LastPayloadHashes|LastInventoryComponentHashes)\b')) 'Runtime must never project or retire the inert legacy queues/maps.'
Require (-not $runtime.Contains('configuration.ServerUrl.TrimEnd')) 'Requests must use the captured bound origin, never the editable next-pair address.'
Require (-not [regex]::IsMatch($runtime, 'Environment\.(MachineName|UserDomainName)|GetMachineId')) 'Enrollment must not collect unused machine identifiers.'
Require (([regex]::Matches($runtime, 'http\.SendAsync\(')).Count -eq 1) 'All HTTP dispatch must pass through one framework-owned permit boundary.'
$dispatchStart = $runtime.IndexOf('private async Task<HttpResponseMessage> SendAsync')
$dispatchEnd = $runtime.IndexOf('private async Task CommitAsync', $dispatchStart)
$dispatch = $runtime.Substring($dispatchStart, $dispatchEnd - $dispatchStart)
Require ($dispatch.Contains('RunOnFrameworkThread') -and $dispatch.IndexOf('RequirePermit(permit)') -lt $dispatch.IndexOf('http.SendAsync')) 'A captured permit must still be current immediately before HTTP dispatch.'
Require ($dispatch.Contains('HttpCompletionOption.ResponseHeadersRead') -and $dispatch.Contains('permit.Cancellation')) 'Requests must stream bounded replies and support lifecycle cancellation.'
Require ($runtime.Contains('await CommitAsync(captured.Permit, state =>')) 'Snapshot and Gil ACKs must commit atomically through their captured owner permit.'
Require (([regex]::Matches($runtime, 'configuration\.Save\(pluginInterface\)')).Count -eq 1) 'Only the framework save coalescer may write configuration.'
Require (-not [regex]::IsMatch($runtime, 'Pending\w+.*RemoveRange|log\.(Error|Warning|Debug)\(error|settingsMessage\s*=\s*error\.Message|RecordDiagnostic\([^\r\n]*error\.Message')) 'Durable eviction and raw remote error sinks must remain absent.'
Require (-not $retainers.Contains('TakeLast(MaxPendingResultEvents)')) 'The 50-event wire batch is not permission to evict older pending evidence.'
Require ($retainers.Contains('sentFingerprints.TryGetValue(entry.EventId') -and $retainers.Contains('sent == entry.PayloadFingerprint')) 'Retainer retirement must match the exact sent fingerprint.'
Require ($runtime.Contains('captured.GilEvents.Chunk(200)') -and $runtime.Contains('GilLedgerPolicy.PreparePayload') -and $runtime.Contains('GilLedgerPolicy.Acknowledge')) 'The real Gil pipeline must validate bounded immutable batches and retire exact versions.'
Require ($runtime.Contains('clientState.Login += OnLogin') -and $runtime.Contains('clientState.Logout += OnLogout') -and $runtime.Contains('requestLifetime.Dispose()')) 'Login/logout and disposal must invalidate active request generations.'
Require ($runtime.Contains('DirectGameSnapshotCollector.ClearTransientState()') -and $collector.Contains('RetainerListingCache.Clear()') -and $collector.Contains('RecentRetainerItems.Clear()')) 'Listings and attribution caches must clear at owner/lifecycle boundaries.'
$logStart = $runtime.IndexOf('private void OnLogMessage')
$chatStart = $runtime.IndexOf('private void OnChatMessage')
$logBody = $runtime.Substring($logStart, $chatStart - $logStart)
Require ($logBody.IndexOf('CanCaptureLedgerEvidence()') -lt $logBody.IndexOf('message.TryGetIntParameter')) 'Consent/login checks must precede log parameter extraction.'
$chatEnd = $runtime.IndexOf('private GilLedgerEvent CreateGilLedgerEvent', $chatStart)
$chatBody = $runtime.Substring($chatStart, $chatEnd - $chatStart)
Require ($chatBody.IndexOf('CanCaptureLedgerEvidence()') -lt $chatBody.IndexOf('OriginalMessage') -and $chatBody.Contains('XivChatType.SystemMessage or XivChatType.RetainerSale')) 'Only authoritative system channels may reach original-message parsing.'
Require ($chatBody.Contains('message.OriginalMessage') -and -not $chatBody.Contains('message.Message.')) 'Sale attribution must use the unmodified message, not another plugin''s rendered text.'
Require ($runtime.Contains('private volatile PluginUiSnapshot uiState') -and $runtime.Contains('QueueUiAction(System.Action action)')) 'Rendering must use an immutable view and queue its configuration changes to the framework.'
Require ($runtime.Contains('GilLedgerPollIntervalMilliseconds = 2000') -and $runtime.Contains('AddMilliseconds(750)')) 'The two-second Gil fallback and 750 ms dirty handling must remain intact.'
Write-Output 'Audit orchestration ownership, consent, response, queue and UI contracts passed.'
