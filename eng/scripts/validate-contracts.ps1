$ErrorActionPreference = 'Stop'
$commonScript = Join-Path $PSScriptRoot 'common.ps1'
. $commonScript

$repoRoot = Get-RepositoryRoot
$toolingDir = Get-ToolingDirectory -RepositoryRoot $repoRoot

Push-Location $toolingDir
try {
  npm run validate:contracts
  if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
  }
}
finally {
  Pop-Location
}
