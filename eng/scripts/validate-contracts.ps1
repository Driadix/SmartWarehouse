$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$toolingDir = Join-Path $repoRoot 'eng\tooling'

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
