$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Get-Content -LiteralPath (Join-Path $root 'GillionsGameSync.csproj') -Raw
$plugin = Get-Content -LiteralPath (Join-Path $root 'Plugin.cs') -Raw
$policy = Get-Content -LiteralPath (Join-Path $root 'RetainerClientPolicy.cs') -Raw

if ($project -notmatch '<Version>1\.0\.30</Version>') {
    throw 'The stable release-candidate version is not 1.0.30.'
}
if ($project -notmatch '<PathMap>\$\(MSBuildProjectDirectory\)=/_/GillionsGameSync</PathMap>') {
    throw 'Release diagnostics no longer sanitize the local source root.'
}
if ($project -notmatch '<RepositoryUrl>\$\(GillionsRepositoryUrl\)</RepositoryUrl>') {
    throw 'Release assembly metadata no longer identifies the canonical public repository.'
}
if ($project -notmatch '<IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>') {
    throw 'Stable packages are no longer reproducible across their final checksum commit.'
}
if ($plugin -notmatch 'EnableAutoRetainerVenturePlans \{ get; set; \} = false;') {
    throw 'Stable upgrade safety requires planner opt-in to default explicitly to false.'
}
if ($plugin -notmatch 'RetainerClientPolicy\.BuildSyncScopes\(SyncScopes, retainerUploadServerSupported\)') {
    throw 'Ordinary scopes are no longer separated from server-accepted Retainer traffic.'
}
if ($plugin -match 'ShouldPollPlans|PollRetainerPlansAsync|ApplyRetainerPlanDelivery|GetIpcSubscriber|InstalledPlugins|ActivePluginsChanged') {
    throw 'Retired plan control or third-party integration remains reachable in the plugin.'
}
if ($plugin -notmatch '#if GILLIONS_TEST_BUILD\s*private const string CommandName = "/gillionssynctest";\s*#else\s*private const string CommandName = "/gillionssync";') {
    throw 'Stable and testing builds must use distinct command names.'
}
if ($plugin -notmatch 'commands\.AddHandler\(CommandName' -or $plugin -notmatch 'commands\.RemoveHandler\(CommandName\)') {
    throw 'The channel-specific command must be registered and released symmetrically.'
}
$presenceStart = $plugin.IndexOf('private void SendCurrentRetainerPresence', [StringComparison]::Ordinal)
$presenceEnd = $plugin.IndexOf('private void OnFrameworkUpdate', $presenceStart, [StringComparison]::Ordinal)
$presenceBody = $plugin.Substring($presenceStart, $presenceEnd - $presenceStart)
if ($presenceBody.IndexOf('ClearRetainerServerAcceptance();', [StringComparison]::Ordinal) -lt 0 -or
    $presenceBody.IndexOf('ClearRetainerServerAcceptance();', [StringComparison]::Ordinal) -ge $presenceBody.IndexOf('_ = SendRetainerPresenceAsync', [StringComparison]::Ordinal)) {
    throw 'Stable Retainer acceptance must clear before heartbeat renewal is dispatched.'
}
if ($policy -notmatch '"GillionsGameSync",\s*"stable",\s*true' -or
    $policy -notmatch '"GillionsGameSyncTest",\s*"testing",\s*false') {
    throw 'Stable and testing product-acceptance policy is not explicit.'
}

Write-Output 'Stable Retainer release-readiness contract passed.'
