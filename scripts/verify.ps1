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
& (Join-Path $root 'tests/stable-readiness-contract-test.ps1')

$stableOutput = Join-Path $root 'artifacts/verification/stable/'
dotnet build $project -c Release --no-restore -warnaserror -p:Version=0.0.0 -p:OutputPath=$stableOutput
if ($LASTEXITCODE -ne 0) { throw 'Stable-compatible Release build failed.' }

$testingOutput = Join-Path $root 'artifacts/verification/testing/'
dotnet build $project -c Release --no-restore -warnaserror -p:GillionsTestBuild=true -p:Version=0.0.0 -p:OutputPath=$testingOutput
if ($LASTEXITCODE -ne 0) { throw 'Testing-compatible Release build failed.' }

Write-Output 'Gillions Game Sync verification passed.'
