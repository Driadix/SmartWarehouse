param(
  [switch]$RemoveVolumes
)

$ErrorActionPreference = 'Stop'
$commonScript = Join-Path $PSScriptRoot 'common.ps1'
. $commonScript

$repoRoot = Get-RepositoryRoot
$envFile = Get-DefaultEnvFile -RepositoryRoot $repoRoot
$composeArguments = @('--env-file', $envFile, 'down', '--remove-orphans')

if ($RemoveVolumes) {
  $composeArguments += '--volumes'
}

Push-Location $repoRoot
try {
  docker compose @composeArguments
  if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
  }
}
finally {
  Pop-Location
}
