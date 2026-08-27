$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Get-Content -LiteralPath (Join-Path $root 'GillionsGameSync.csproj') -Raw
$plugin = Get-Content -LiteralPath (Join-Path $root 'Plugin.cs') -Raw
$policy = Get-Content -LiteralPath (Join-Path $root 'RetainerClientPolicy.cs') -Raw

if ($project -notmatch '<Version>1\.0\.29</Version>') {
    throw 'The stable release-candidate version is not 1.0.29.'
}
if ($project -notmatch '<PathMap>\$\(MSBuildProjectDirectory\)=/_/GillionsGameSync</PathMap>') {
    throw 'Release diagnostics no longer sanitize the local source root.'
}
if ($plugin -notmatch 'EnableAutoRetainerVenturePlans \{ get; set; \} = false;') {
    throw 'Stable upgrade safety requires planner opt-in to default explicitly to false.'
}
if ($plugin -notmatch 'RetainerClientPolicy\.BuildSyncScopes\(SyncScopes, retainerUploadServerSupported\)') {
    throw 'Ordinary scopes are no longer separated from server-accepted Retainer traffic.'
}
if ($plugin -notmatch 'RetainerClientPolicy\.ShouldPollPlans\(') {
    throw 'Plan polling is no longer guarded by the shared eligibility policy.'
}
if ($plugin -notmatch '#if GILLIONS_TEST_BUILD\s*private const string CommandName = "/gillionssynctest";\s*#else\s*private const string CommandName = "/gillionssync";') {
    throw 'Stable and testing builds must use distinct command names.'
}
if ($plugin -notmatch 'commands\.AddHandler\(CommandName' -or $plugin -notmatch 'commands\.RemoveHandler\(CommandName\)') {
    throw 'The channel-specific command must be registered and released symmetrically.'
}
if ($plugin -notmatch 'A missing or\s*// malformed response cannot leave a previous server grant active\.\s*ClearRetainerServerAcceptance\(\);') {
    throw 'Stable Retainer acceptance is not cleared before heartbeat renewal.'
}
if ($policy -notmatch '"GillionsGameSync",\s*"stable",\s*true' -or
    $policy -notmatch '"GillionsGameSyncTest",\s*"testing",\s*false') {
    throw 'Stable and testing product-acceptance policy is not explicit.'
}

Write-Output 'Stable Retainer release-readiness contract passed.'
