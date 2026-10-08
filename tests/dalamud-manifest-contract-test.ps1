param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $root 'data/GillionsGameSync.json'
$canonicalRepositoryUrl = 'https://github.com/anndrox/GillionsGameSync'
$canonicalRawUrl = 'https://raw.githubusercontent.com/anndrox/GillionsGameSync/main/data/GillionsGameSync.json'
$requiredFields = @(
  'Author', 'Name', 'InternalName', 'AssemblyVersion', 'Description',
  'ApplicableVersion', 'RepoUrl', 'Tags', 'DalamudApiLevel',
  'LoadRequiredState', 'LoadSync', 'CanUnloadAsync', 'LoadPriority',
  'Punchline', 'AcceptsFeedback', 'IconUrl', 'DownloadLink',
  'DownloadLinkInstall', 'DownloadLinkUpdate', 'DownloadLinkTesting',
  'DownloadCount', 'LastUpdate'
)

function Assert-Condition([bool]$Condition, [string]$Message) {
  if (-not $Condition) { throw $Message }
}

function Test-StableManifest([string]$Json) {
  Assert-Condition ($Json.Length -le 65536) 'Manifest exceeds the 64 KiB size limit.'
  try {
    $document = [Text.Json.JsonDocument]::Parse($Json)
    Assert-Condition ($document.RootElement.ValueKind -eq [Text.Json.JsonValueKind]::Array) 'Manifest root must be an array.'
    $parsed = @(ConvertFrom-Json -InputObject $Json -AsHashtable)
  }
  catch { throw "Manifest is not valid JSON: $($_.Exception.Message)" }
  finally { if ($document) { $document.Dispose() } }

  Assert-Condition ($parsed.Count -eq 1) 'Manifest must contain exactly one repository entry.'
  $entry = $parsed[0]
  Assert-Condition ($entry -is [Collections.IDictionary]) 'Manifest entry must be an object.'
  foreach ($field in $requiredFields) {
    Assert-Condition ($entry.Contains($field)) "Manifest is missing required field $field."
  }

  Assert-Condition ($entry.InternalName -ceq 'GillionsGameSync') 'InternalName must be GillionsGameSync.'
  Assert-Condition ($entry.Name -ceq 'Gillions Game Sync') 'Name must be Gillions Game Sync.'
  Assert-Condition ($entry.AssemblyVersion -is [string] -and $entry.AssemblyVersion -match '^\d+\.\d+\.\d+\.\d+$') 'AssemblyVersion must be a four-part stable version.'
  Assert-Condition ($entry.DalamudApiLevel -is [long] -and $entry.DalamudApiLevel -gt 0) 'DalamudApiLevel must be a positive integer.'
  Assert-Condition ($entry.LoadSync -is [bool] -and $entry.CanUnloadAsync -is [bool] -and $entry.AcceptsFeedback -is [bool]) 'Manifest boolean fields must remain booleans.'
  Assert-Condition ($entry.Tags -is [object[]] -and $entry.Tags.Count -gt 0) 'Tags must be a non-empty array.'
  Assert-Condition ($entry.RepoUrl -ceq $canonicalRepositoryUrl) 'RepoUrl must identify the public GitHub repository.'
  Assert-Condition ($entry.LastUpdate -is [long] -and $entry.LastUpdate -gt 0) 'LastUpdate must be a positive Unix timestamp.'

  $version = [regex]::Match($entry.DownloadLink, '^https://github\.com/anndrox/GillionsGameSync/releases/download/v(?<version>\d+\.\d+\.\d+(?:\.\d+)?)/').Groups['version'].Value
  Assert-Condition ($version.Length -gt 0) 'DownloadLink must identify a three- or four-part immutable release version.'
  $assemblyVersion = if ($version.Split('.').Count -eq 3) { "$version.0" } else { $version }
  Assert-Condition ($entry.AssemblyVersion -ceq $assemblyVersion) 'Release and assembly versions disagree.'
  $expectedDownload = "https://github.com/anndrox/GillionsGameSync/releases/download/v$version/GillionsGameSync-$version.zip"
  foreach ($field in @('DownloadLink', 'DownloadLinkInstall', 'DownloadLinkUpdate', 'DownloadLinkTesting')) {
    Assert-Condition ($entry[$field] -ceq $expectedDownload) "$field must reference the immutable stable $version ZIP."
  }
  Assert-Condition ($entry.IconUrl -ceq 'https://raw.githubusercontent.com/anndrox/GillionsGameSync/main/assets/GillionsGameSync-icon-v4.png') 'IconUrl must reference the reviewed public GitHub icon.'
  Assert-Condition (-not $entry.Contains('TestingAssemblyVersion') -and -not $entry.Contains('TestingDalamudApiLevel')) 'The stable manifest must not advertise an in-entry testing build.'

  foreach ($field in @('RepoUrl', 'IconUrl', 'DownloadLink', 'DownloadLinkInstall', 'DownloadLinkUpdate', 'DownloadLinkTesting')) {
    $uri = [Uri]$entry[$field]
    Assert-Condition ($uri.Scheme -eq 'https' -and -not $uri.UserInfo -and -not $uri.Query) "$field must be anonymous HTTPS without credentials or query data."
  }

  $forbidden = @('token', 'secret', 'password', 'credential', 'accountId', 'characterId', 'machineId', 'retainer')
  foreach ($key in $entry.Keys) {
    Assert-Condition ($key -notin $forbidden) "Manifest contains forbidden private field $key."
  }
}

function Assert-Rejected([string]$Json, [string]$CaseName) {
  try { Test-StableManifest $Json; throw "Invalid fixture '$CaseName' was accepted." }
  catch {
    if ($_.Exception.Message -eq "Invalid fixture '$CaseName' was accepted.") { throw }
  }
}

$json = [IO.File]::ReadAllText($manifestPath)
Test-StableManifest $json

Assert-Rejected '{' 'malformed JSON'
Assert-Rejected '{}' 'schema mismatch'
$missing = @($json | ConvertFrom-Json -AsHashtable)
$missing[0].Remove('DownloadLinkUpdate')
Assert-Rejected ($missing | ConvertTo-Json -Depth 8) 'missing required field'
$wrongIdentity = $json.Replace('"GillionsGameSync"', '"ForeignPlugin"')
Assert-Rejected $wrongIdentity 'wrong plugin identity'
$manifestVersion = [regex]::Match(@($json | ConvertFrom-Json -AsHashtable)[0].DownloadLink, '/v(?<version>\d+\.\d+\.\d+(?:\.\d+)?)/').Groups['version'].Value
$privateQuery = $json.Replace("GillionsGameSync-$manifestVersion.zip`"", "GillionsGameSync-$manifestVersion.zip?characterId=1`"")
Assert-Rejected $privateQuery 'private query data'

# Synthetic identities only: do not run any release tool or touch the active feed.
foreach ($version in @('1.0.30', '1.0.31.1', '1.0.31.0')) {
  $fixture = @($json | ConvertFrom-Json -AsHashtable)
  $fixture[0].AssemblyVersion = if ($version.Split('.').Count -eq 3) { "$version.0" } else { $version }
  foreach ($field in @('DownloadLink', 'DownloadLinkInstall', 'DownloadLinkUpdate', 'DownloadLinkTesting')) {
    $fixture[0][$field] = "https://github.com/anndrox/GillionsGameSync/releases/download/v$version/GillionsGameSync-$version.zip"
  }
  Test-StableManifest (ConvertTo-Json -InputObject $fixture -Depth 8)
  $fixture[0].AssemblyVersion = "$version.0"
  if ($version.Split('.').Count -eq 4) { Assert-Rejected (ConvertTo-Json -InputObject $fixture -Depth 8) 'spurious fifth component' }
}

# Evaluate only parameter validation and the pure normalization AST, never tools.
foreach ($script in @('package.ps1', 'prepare-stable-github-release.ps1', 'publish-stable-github-release.ps1', 'verify-public-stable-release.ps1')) {
  $tokens = $null; $parseErrors = $null
  $ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $root "scripts/$script"), [ref]$tokens, [ref]$parseErrors)
  Assert-Condition ($parseErrors.Count -eq 0) "$script must parse."
  $parameter = $ast.ParamBlock.Parameters | Where-Object { $_.Name.VariablePath.UserPath -ceq 'Version' }
  $pattern = ($parameter.Attributes | Where-Object { $_.TypeName.Name -ceq 'ValidatePattern' }).PositionalArguments[0].Value
  foreach ($version in @('1.0.30', '1.0.31.1')) { Assert-Condition ($version -match $pattern) "$script rejects supported identity $version." }
  Assert-Condition ('1.0.31.1.0' -notmatch $pattern) "$script admits a fifth component."
  if ($script -ne 'prepare-stable-github-release.ps1') {
    $normalization = $ast.Find({ param($node) $node -is [Management.Automation.Language.AssignmentStatementAst] -and $node.Left.Extent.Text -ceq '$assemblyVersion' }, $true)
    foreach ($version in @('1.0.30', '1.0.31.1')) {
      $assemblyVersion = $null
      . ([scriptblock]::Create($normalization.Extent.Text))
      $expected = if ($version -ceq '1.0.30') { '1.0.30.0' } else { '1.0.31.1' }
      Assert-Condition ($assemblyVersion -ceq $expected) "$script normalization failed for $version."
    }
  }
}

Assert-Condition (-not $json.Contains('gillions.app/downloads/plugins/')) 'The active stable manifest must not depend on Gillions-hosted artifacts.'

$readme = [IO.File]::ReadAllText((Join-Path $root 'README.md'))
Assert-Condition ($readme.Contains($canonicalRawUrl)) 'README must publish the canonical raw GitHub manifest URL.'
Assert-Condition (-not $readme.Contains('https://gillions.app/plugins/GillionsGameSync.json')) 'README must not advertise the retired Gillions-hosted stable manifest URL.'

Write-Output 'Dalamud manifest contract verification passed.'
