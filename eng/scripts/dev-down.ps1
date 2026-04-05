$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')

Push-Location $repoRoot
try {
  docker compose down --remove-orphans
}
finally {
  Pop-Location
}
