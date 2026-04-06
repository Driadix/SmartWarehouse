$ErrorActionPreference = 'Stop'
$commonScript = Join-Path $PSScriptRoot 'common.ps1'
. $commonScript

$repoRoot = Get-RepositoryRoot

Push-Location $repoRoot
try {
  Invoke-RepoDotnet -RepositoryRoot $repoRoot -InstallIfMissing -Arguments @('test', 'SmartWarehouse.slnx', '--filter', 'Category!=Manual')
}
finally {
  Pop-Location
}
