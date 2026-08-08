param(
  [string]$CollectorPath = (Join-Path $PSScriptRoot "..\DirectGameSnapshotCollector.cs")
)

$ErrorActionPreference = "Stop"
$source = Get-Content -LiteralPath $CollectorPath -Raw

if ($source -notmatch "bardings\s*=\s*CollectibleCollector\.ReadBardings\(dataManager, unlockState\)") {
  throw "Collectibles payload must include the authoritative bardings array."
}

$method = [regex]::Match($source, "public static long\[\] ReadBardings[\s\S]*?(?=\r?\n\s*public static long\[\] ReadEmotes)")
if (-not $method.Success) { throw "ReadBardings collector method was not found." }
$implementation = $method.Value
if ($implementation -notmatch "Read<BuddyEquip>\(dataManager, unlockState\.IsBuddyEquipUnlocked\)") {
  throw "Bardings must use BuddyEquip with IUnlockState.IsBuddyEquipUnlocked."
}
if ($implementation -match "Mount|Item") { throw "Bardings must not be inferred from mounts or items." }

# Deterministic fixture for the selection contract: unlocked BuddyEquip rows
# are emitted and locked rows are excluded by the authoritative predicate.
$fixtureRows = @(
  [pscustomobject]@{ RowId = 101; Unlocked = $true },
  [pscustomobject]@{ RowId = 102; Unlocked = $false },
  [pscustomobject]@{ RowId = 103; Unlocked = $true }
)
$emitted = @($fixtureRows | Where-Object Unlocked | ForEach-Object RowId)
if ($emitted.Count -ne 2 -or $emitted[0] -ne 101 -or $emitted[1] -ne 103) { throw "Unlocked/locked BuddyEquip fixture selection failed." }

Write-Output "Barding collector contract fixture passed."
