$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$localEnvFile = Join-Path $repoRoot '.env'
$exampleEnvFile = Join-Path $repoRoot '.env.example'
$envFile = if (Test-Path $localEnvFile) { $localEnvFile } else { $exampleEnvFile }

Push-Location $repoRoot
try {
  docker compose --env-file $envFile up -d
}
finally {
  Pop-Location
}
