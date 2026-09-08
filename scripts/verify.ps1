param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'GillionsGameSync.csproj'
$tests = Join-Path $root 'tests/GillionsGameSync.ItemLinkTests/GillionsGameSync.ItemLinkTests.csproj'

dotnet restore $project --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Plugin restore failed.' }

dotnet run --project $tests -c Release
if ($LASTEXITCODE -ne 0) { throw 'Focused fixture executable failed.' }

& (Join-Path $root 'tests/dalamud-manifest-contract-test.ps1')
& (Join-Path $root 'tests/package-manifest-contract-test.ps1')
& (Join-Path $root 'tests/bardings-collector-contract-test.ps1')
& (Join-Path $root 'tests/folklore-collector-contract-test.ps1')
& (Join-Path $root 'tests/performance-contract-test.ps1')
& (Join-Path $root 'tests/audit-orchestration-contract-test.ps1')
& (Join-Path $root 'tests/stable-readiness-contract-test.ps1')

$stableOutput = Join-Path $root 'artifacts/verification/stable/'
dotnet build $project -c Release --no-restore -warnaserror -p:Version=0.0.0 -p:OutputPath=$stableOutput
if ($LASTEXITCODE -ne 0) { throw 'Stable-compatible Release build failed.' }

$testingOutput = Join-Path $root 'artifacts/verification/testing/'
dotnet build $project -c Release --no-restore -warnaserror -p:GillionsTestBuild=true -p:Version=0.0.0 -p:OutputPath=$testingOutput
if ($LASTEXITCODE -ne 0) { throw 'Testing-compatible Release build failed.' }

$configurationTests = Join-Path $root 'tests/GillionsGameSync.ConfigurationTests/GillionsGameSync.ConfigurationTests.csproj'
$dalamudPath = dotnet msbuild $project -getProperty:DalamudLibPath -nologo
if ($LASTEXITCODE -ne 0) { throw 'Unable to identify the actual Dalamud serializer libraries.' }
foreach ($channel in @('stable', 'testing')) {
    $assemblyName = if ($channel -eq 'stable') { 'GillionsGameSync.dll' } else { 'GillionsGameSyncTest.dll' }
    $binary = Join-Path $root "artifacts/verification/$channel/$assemblyName"
    $fixtureDirectory = Join-Path $root "artifacts/verification/configuration-fixtures/$channel"
    dotnet run --project $configurationTests -c Release -- $binary $dalamudPath $fixtureDirectory
    if ($LASTEXITCODE -ne 0) { throw "$channel actual-serializer preservation fixtures failed." }
}

Write-Output 'Gillions Game Sync verification passed.'
