param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'GillionsGameSync.csproj'
$tests = Join-Path $root 'tests/GillionsGameSync.ItemLinkTests/GillionsGameSync.ItemLinkTests.csproj'

dotnet restore $project --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Plugin restore failed.' }

dotnet run --project $tests -c Release
if ($LASTEXITCODE -ne 0) { throw 'Focused fixture executable failed.' }

$partyFinderTests = Join-Path $root 'tests/GillionsGameSync.PartyFinderTests/GillionsGameSync.PartyFinderTests.csproj'
dotnet run --project $partyFinderTests -c Release
if ($LASTEXITCODE -ne 0) { throw 'Party Finder contribution behavior fixture failed.' }

dotnet run --project (Join-Path $root 'tests/GillionsGameSync.HuntMapTests/GillionsGameSync.HuntMapTests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Hunt map contract/lifecycle/race fixtures failed.' }
python (Join-Path $root 'tests/hunt-map-contract-test.py')
if ($LASTEXITCODE -ne 0) { throw 'Hunt map source boundaries failed.' }

$submarineTests = Join-Path $root 'tests/GillionsGameSync.SubmarineTests/GillionsGameSync.SubmarineTests.csproj'
dotnet run --project $submarineTests -c Release -- --fixture (Join-Path $root 'artifacts/verification/submarine-policy/retained-fixture.json')
if ($LASTEXITCODE -ne 0) { throw 'Submarine retention/consent fixtures failed.' }
python (Join-Path $root 'tests/submarine-contract-test.py')
if ($LASTEXITCODE -ne 0) { throw 'Submarine source-boundary fixtures failed.' }
dotnet run --project (Join-Path $root 'tests/GillionsGameSync.PersonalStateTests/GillionsGameSync.PersonalStateTests.csproj') -c Release -- --fixture (Join-Path $root 'artifacts/verification/personal-state/hunt-bills-v1.json')
if ($LASTEXITCODE -ne 0) { throw 'Hunt/private-state/patch fixtures failed.' }
python (Join-Path $root 'tests/personal-state-contract-test.py')
if ($LASTEXITCODE -ne 0) { throw 'Personal-state source contracts failed.' }

dotnet run --project (Join-Path $root 'tests/GillionsGameSync.DashboardTests/GillionsGameSync.DashboardTests.csproj') -c Release -- --fixture (Join-Path $root 'artifacts/verification/dashboard/dashboard-facts-v1.json')
if ($LASTEXITCODE -ne 0) { throw 'Dashboard private-fact policy fixtures failed.' }
python (Join-Path $root 'tests/dashboard-contract-test.py')
if ($LASTEXITCODE -ne 0) { throw 'Dashboard source/fixture boundaries failed.' }

dotnet run --project (Join-Path $root 'tests/GillionsGameSync.TravelTests/GillionsGameSync.TravelTests.csproj') -c Release -- --fixture (Join-Path $root 'artifacts/verification/travel/travel-context-v1.json')
if ($LASTEXITCODE -ne 0) { throw 'Ephemeral private travel policy fixtures failed.' }
python (Join-Path $root 'tests/travel-context-contract-test.py')
if ($LASTEXITCODE -ne 0) { throw 'Travel read/consent/retention/source boundaries failed.' }

dotnet run --project (Join-Path $root 'tests/GillionsGameSync.MarketTests/GillionsGameSync.MarketTests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Passive market event/transport fixtures failed.' }
python (Join-Path $root 'tests/market-contribution-contract-test.py')
if ($LASTEXITCODE -ne 0) { throw 'Passive market source boundaries failed.' }

& (Join-Path $root 'tests/dalamud-manifest-contract-test.ps1')
& (Join-Path $root 'tests/package-manifest-contract-test.ps1')
& (Join-Path $root 'tests/bardings-collector-contract-test.ps1')
& (Join-Path $root 'tests/folklore-collector-contract-test.ps1')
& (Join-Path $root 'tests/performance-contract-test.ps1')
& (Join-Path $root 'tests/audit-orchestration-contract-test.ps1')
& (Join-Path $root 'tests/stable-readiness-contract-test.ps1')
& (Join-Path $root 'tests/party-finder-contribution-contract-test.ps1')

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
