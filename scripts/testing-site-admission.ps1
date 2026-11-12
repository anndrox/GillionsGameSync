function Assert-TestingSiteAdmission {
  param(
    [Parameter(Mandatory = $true)][ValidatePattern('^\d+\.\d+\.\d+\.\d+$')][string]$Version,
    [Parameter(Mandatory = $true)][ValidatePattern('^\d{4}\.\d{2}\.\d{2}\.\d{4}\.\d{4}$')][string]$GameBuild
  )

  $origin = 'https://test.gillions.app'
  $product = 'GillionsGameSyncTest'
  $uri = "$origin/api/game-sync/testing-release-admission?product=$product&version=$Version&gameBuild=$GameBuild"
  try {
    $headers = @{ Accept = 'application/json'; 'Cache-Control' = 'no-cache' }
    $response = Invoke-WebRequest -Uri $uri -Method Get -MaximumRedirection 0 -TimeoutSec 15 -ErrorAction Stop -Headers $headers
    if ($response.StatusCode -ne 200 -or $response.Content.Length -gt 4096) { throw 'Invalid admission response.' }
    $admission = ConvertFrom-Json -InputObject $response.Content -AsHashtable -Depth 8 -ErrorAction Stop
  } catch {
    throw "Testing publication requires a live admission response from test.gillions.app for $Version on $GameBuild."
  }

  if ($admission -isnot [System.Collections.IDictionary] -or
      $admission.ok -isnot [bool] -or -not $admission.ok -or
      $admission.admitted -isnot [bool] -or -not $admission.admitted -or
      $admission.origin -isnot [string] -or $admission.origin -cne $origin -or
      $admission.product -isnot [string] -or $admission.product -cne $product -or
      $admission.version -isnot [string] -or $admission.version -cne $Version -or
      $admission.currentGameBuild -isnot [string] -or $admission.currentGameBuild -cne $GameBuild) {
    throw "TEST has not confirmed exact admission for $Version on the current game build $GameBuild."
  }
}
