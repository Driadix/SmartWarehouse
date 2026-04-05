$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$repoDotnetDir = Join-Path $repoRoot '.dotnet'
$repoDotnet = Join-Path $repoDotnetDir 'dotnet.exe'
$installScript = Join-Path $PSScriptRoot 'dotnet-install.ps1'

if (-not (Test-Path $repoDotnet)) {
  New-Item -ItemType Directory -Force -Path $repoDotnetDir | Out-Null
  & powershell -ExecutionPolicy Bypass -File $installScript -Version 10.0.200 -InstallDir $repoDotnetDir -NoPath
}

Push-Location $repoRoot
try {
  & $repoDotnet restore SmartWarehouse.slnx

  Push-Location (Join-Path $repoRoot 'eng\tooling')
  try {
    if (Test-Path 'package-lock.json') {
      npm ci
    }
    else {
      npm install
    }
  }
  finally {
    Pop-Location
  }
}
finally {
  Pop-Location
}
