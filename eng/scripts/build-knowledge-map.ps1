$ErrorActionPreference = 'Stop'
$commonScript = Join-Path $PSScriptRoot 'common.ps1'
. $commonScript

$repoRoot = Get-RepositoryRoot
$toolingDir = Get-ToolingDirectory -RepositoryRoot $repoRoot

Push-Location $toolingDir
try {
  node ./build-knowledge-map.mjs
  if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
  }
}
finally {
  Pop-Location
}
