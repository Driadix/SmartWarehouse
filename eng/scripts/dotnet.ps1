param(
  [Parameter(ValueFromRemainingArguments = $true)]
  [string[]]$Arguments
)

$ErrorActionPreference = 'Stop'
$commonScript = Join-Path $PSScriptRoot 'common.ps1'
. $commonScript

$repoRoot = Get-RepositoryRoot
Invoke-RepoDotnet -RepositoryRoot $repoRoot -InstallIfMissing -Arguments $Arguments
