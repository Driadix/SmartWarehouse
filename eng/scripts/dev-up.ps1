$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')

Push-Location $repoRoot
try {
  docker compose --env-file .env.example up -d
}
finally {
  Pop-Location
}
