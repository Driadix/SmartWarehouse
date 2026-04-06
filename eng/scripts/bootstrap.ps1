$ErrorActionPreference = 'Stop'
$commonScript = Join-Path $PSScriptRoot 'common.ps1'
. $commonScript

$repoRoot = Get-RepositoryRoot
$toolingDir = Get-ToolingDirectory -RepositoryRoot $repoRoot

Push-Location $repoRoot
try {
  Invoke-RepoDotnet -RepositoryRoot $repoRoot -InstallIfMissing -Arguments @('restore', 'SmartWarehouse.slnx')

  $nodeCommand = Get-Command node -ErrorAction SilentlyContinue
  if ($null -eq $nodeCommand) {
    $expectedNodeVersion = Get-ExpectedNodeVersion -RepositoryRoot $repoRoot
    throw "Node.js $expectedNodeVersion is required. Use .nvmrc to install the pinned version."
  }

  $expectedNodeVersion = Get-ExpectedNodeVersion -RepositoryRoot $repoRoot
  $actualNodeVersion = (& $nodeCommand.Source --version).Trim().TrimStart('v')
  if ($actualNodeVersion -ne $expectedNodeVersion) {
    Write-Warning "Expected Node.js $expectedNodeVersion, but found $actualNodeVersion."
  }

  Push-Location $toolingDir
  try {
    if (Test-Path 'package-lock.json') {
      npm ci
    }
    else {
      npm install
    }

    if ($LASTEXITCODE -ne 0) {
      exit $LASTEXITCODE
    }
  }
  finally {
    Pop-Location
  }
}
finally {
  Pop-Location
}
