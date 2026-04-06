Set-StrictMode -Version Latest

function Test-IsWindowsPlatform {
  $isWindowsVariable = Get-Variable -Name IsWindows -ErrorAction SilentlyContinue
  if ($null -ne $isWindowsVariable) {
    return [bool]$isWindowsVariable.Value
  }

  return $env:OS -eq 'Windows_NT'
}

function Get-RepositoryRoot {
  return (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
}

function Get-ToolingDirectory {
  param(
    [string]$RepositoryRoot = (Get-RepositoryRoot)
  )

  return Join-Path $RepositoryRoot 'eng\tooling'
}

function Get-DefaultEnvFile {
  param(
    [string]$RepositoryRoot = (Get-RepositoryRoot)
  )

  $localEnvFile = Join-Path $RepositoryRoot '.env'
  if (Test-Path $localEnvFile) {
    return $localEnvFile
  }

  return Join-Path $RepositoryRoot '.env.example'
}

function Get-ExpectedDotnetSdkVersion {
  param(
    [string]$RepositoryRoot = (Get-RepositoryRoot)
  )

  $globalJsonPath = Join-Path $RepositoryRoot 'global.json'
  return (Get-Content $globalJsonPath -Raw | ConvertFrom-Json).sdk.version
}

function Get-ExpectedNodeVersion {
  param(
    [string]$RepositoryRoot = (Get-RepositoryRoot)
  )

  return (Get-Content (Join-Path $RepositoryRoot '.nvmrc') -Raw).Trim().TrimStart('v')
}

function Get-RepositoryDotnetPath {
  param(
    [string]$RepositoryRoot = (Get-RepositoryRoot)
  )

  $dotnetExecutable = if (Test-IsWindowsPlatform) { 'dotnet.exe' } else { 'dotnet' }
  return Join-Path (Join-Path $RepositoryRoot '.dotnet') $dotnetExecutable
}

function Install-RepositoryDotnet {
  param(
    [string]$RepositoryRoot = (Get-RepositoryRoot)
  )

  $installScript = Join-Path $PSScriptRoot 'dotnet-install.ps1'
  $installDir = Join-Path $RepositoryRoot '.dotnet'
  $sdkVersion = Get-ExpectedDotnetSdkVersion -RepositoryRoot $RepositoryRoot

  New-Item -ItemType Directory -Force -Path $installDir | Out-Null

  & $installScript -Version $sdkVersion -InstallDir $installDir -NoPath
  if ($LASTEXITCODE -ne 0) {
    throw "Unable to install .NET SDK $sdkVersion into $installDir."
  }

  return Get-RepositoryDotnetPath -RepositoryRoot $RepositoryRoot
}

function Resolve-DotnetCommand {
  param(
    [string]$RepositoryRoot = (Get-RepositoryRoot),
    [switch]$InstallIfMissing
  )

  $repositoryDotnet = Get-RepositoryDotnetPath -RepositoryRoot $RepositoryRoot
  if (Test-Path $repositoryDotnet) {
    return $repositoryDotnet
  }

  $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
  if ($null -ne $dotnetCommand) {
    return $dotnetCommand.Source
  }

  if ($InstallIfMissing) {
    return Install-RepositoryDotnet -RepositoryRoot $RepositoryRoot
  }

  throw "dotnet was not found. Run eng/scripts/bootstrap.ps1 or install the SDK version from global.json."
}

function Invoke-RepoDotnet {
  param(
    [Parameter(Mandatory = $true)]
    [string[]]$Arguments,

    [string]$RepositoryRoot = (Get-RepositoryRoot),

    [switch]$InstallIfMissing
  )

  $dotnet = Resolve-DotnetCommand -RepositoryRoot $RepositoryRoot -InstallIfMissing:$InstallIfMissing
  & $dotnet @Arguments
  if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
  }
}

function Get-ComposeProfileArguments {
  param(
    [string[]]$Profiles
  )

  $arguments = @()

  foreach ($profile in $Profiles) {
    if ([string]::IsNullOrWhiteSpace($profile)) {
      continue
    }

    $arguments += @('--profile', $profile)
  }

  return $arguments
}
