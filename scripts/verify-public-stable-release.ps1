param(
  [string]$ManifestUrl = 'https://raw.githubusercontent.com/anndrox/GillionsGameSync/main/data/GillionsGameSync.json',
  [string]$Version = '1.0.29'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$expectedAssetName = "GillionsGameSync-$Version.zip"
$expectedAssetUrl = "https://github.com/anndrox/GillionsGameSync/releases/download/v$Version/$expectedAssetName"
$expectedIconUrl = 'https://raw.githubusercontent.com/anndrox/GillionsGameSync/main/assets/GillionsGameSync-icon-v4.png'
$checksumPath = Join-Path $root "data/releases/v$Version.sha256"
$expectedHash = ([IO.File]::ReadAllText($checksumPath).Split(' ', [StringSplitOptions]::RemoveEmptyEntries)[0]).ToLowerInvariant()
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("gillions-release-verify-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null

function Assert-Condition([bool]$Condition, [string]$Message) {
  if (-not $Condition) { throw $Message }
}

try {
  $manifestResponse = Invoke-WebRequest -UseBasicParsing -Uri $ManifestUrl
  Assert-Condition ($manifestResponse.StatusCode -eq 200) 'The public stable manifest did not return HTTP 200.'
  $entries = @($manifestResponse.Content | ConvertFrom-Json -AsHashtable)
  Assert-Condition ($entries.Count -eq 1) 'The public stable manifest must contain exactly one entry.'
  $entry = $entries[0]
  Assert-Condition ($entry.InternalName -ceq 'GillionsGameSync') 'The public manifest has the wrong plugin identity.'
  Assert-Condition ($entry.AssemblyVersion -ceq "$Version.0") 'The public manifest has the wrong stable version.'
  Assert-Condition ($entry.RepoUrl -ceq 'https://github.com/anndrox/GillionsGameSync') 'RepoUrl is not the canonical GitHub repository.'
  Assert-Condition ($entry.IconUrl -ceq $expectedIconUrl) 'IconUrl is not the canonical GitHub icon.'
  foreach ($field in @('DownloadLink', 'DownloadLinkInstall', 'DownloadLinkUpdate', 'DownloadLinkTesting')) {
    Assert-Condition ($entry[$field] -ceq $expectedAssetUrl) "$field does not resolve to the immutable GitHub Release asset."
    $uri = [Uri]$entry[$field]
    Assert-Condition (-not $uri.Query -and -not $uri.UserInfo) "$field contains private or mutable request data."
  }
  Assert-Condition (-not $entry.Contains('TestingAssemblyVersion') -and -not $entry.Contains('TestingDalamudApiLevel')) 'Stable manifest unexpectedly advertises an in-entry testing version.'

  $iconPath = Join-Path $tempRoot 'icon.png'
  $iconResponse = Invoke-WebRequest -UseBasicParsing -Uri $expectedIconUrl -OutFile $iconPath -PassThru
  Assert-Condition ($iconResponse.StatusCode -eq 200) 'The public GitHub icon did not return HTTP 200.'
  $signature = [IO.File]::ReadAllBytes($iconPath)[0..7]
  Assert-Condition (($signature -join ',') -ceq '137,80,78,71,13,10,26,10') 'The public icon is not a valid PNG.'

  $zipPath = Join-Path $tempRoot $expectedAssetName
  $zipResponse = Invoke-WebRequest -UseBasicParsing -Uri $expectedAssetUrl -OutFile $zipPath -PassThru
  Assert-Condition ($zipResponse.StatusCode -eq 200) 'The GitHub Release asset did not download successfully through redirects.'
  $actualHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
  Assert-Condition ($actualHash -ceq $expectedHash) "GitHub Release asset SHA-256 mismatch: expected $expectedHash, received $actualHash."

  Add-Type -AssemblyName System.IO.Compression.FileSystem
  $archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
  try {
    $names = @($archive.Entries | ForEach-Object FullName | Sort-Object)
    $expectedNames = @('GillionsGameSync.deps.json', 'GillionsGameSync.dll', 'GillionsGameSync.json')
    Assert-Condition (($names -join '|') -ceq ($expectedNames -join '|')) 'The release ZIP does not contain the expected three-file Dalamud package.'
    $embeddedEntry = $archive.GetEntry('GillionsGameSync.json')
    $reader = [IO.StreamReader]::new($embeddedEntry.Open())
    try { $embedded = $reader.ReadToEnd() | ConvertFrom-Json -AsHashtable }
    finally { $reader.Dispose() }
    Assert-Condition ($embedded.InternalName -ceq 'GillionsGameSync') 'Embedded manifest has the wrong plugin identity.'
    Assert-Condition ($embedded.AssemblyVersion -ceq "$Version.0") 'Embedded manifest has the wrong plugin version.'
  }
  finally { $archive.Dispose() }

  Write-Output "Public stable release verification passed: v$Version $actualHash"
}
finally {
  if (Test-Path -LiteralPath $tempRoot) { Remove-Item -LiteralPath $tempRoot -Recurse -Force }
}
