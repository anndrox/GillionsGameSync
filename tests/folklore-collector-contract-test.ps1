$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$collector = Get-Content (Join-Path $root 'DirectGameSnapshotCollector.cs') -Raw

if ($collector -notmatch 'folkloreBookIds\s*=\s*CollectibleCollector\.ReadFolkloreBookIds') {
    throw 'The complete Collectibles payload does not expose folkloreBookIds.'
}
if ($collector -notmatch 'SheetRowCache<GatheringSubCategory>') {
    throw 'Folklore collection must use authoritative GatheringSubCategory rows.'
}
if ($collector -notmatch 'IsFolkloreBookUnlocked\(\(uint\)row\.RowId\)') {
    throw 'Folklore ownership must use PlayerState.IsFolkloreBookUnlocked.'
}
if ($collector -notmatch 'Select\(row => \(long\)row\.Item\.RowId\)') {
    throw 'Folklore payload IDs must be stable tome Item row IDs.'
}
if ($collector -notmatch '\.Distinct\(\)\s*\.Order\(\)') {
    throw 'Folklore tome Item IDs must be deduplicated and deterministic.'
}

Write-Output 'Folklore collector contract passed.'
