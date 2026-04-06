param(
  [string[]]$Profile = @('infra', 'observability'),

  [switch]$IncludeApps,

  [switch]$Build
)

$ErrorActionPreference = 'Stop'
$commonScript = Join-Path $PSScriptRoot 'common.ps1'
. $commonScript

$repoRoot = Get-RepositoryRoot
$envFile = Get-DefaultEnvFile -RepositoryRoot $repoRoot
$profiles = @($Profile)

if ($IncludeApps -and $profiles -notcontains 'apps') {
  $profiles += 'apps'
}

$composeArguments = @('--env-file', $envFile)
$composeArguments += Get-ComposeProfileArguments -Profiles $profiles
$composeArguments += 'up'
$composeArguments += '-d'

if ($Build) {
  $composeArguments += '--build'
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
