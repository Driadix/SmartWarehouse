$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$repoDotnet = Join-Path $repoRoot '.dotnet\dotnet.exe'

Push-Location $repoRoot
try {
  & $repoDotnet test SmartWarehouse.slnx --filter Category!=Manual
}
finally {
  Pop-Location
}
