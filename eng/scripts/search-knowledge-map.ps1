param(
  [Parameter(Mandatory = $true)]
  [string]$Query,

  [int]$Top = 10
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$mapPath = Join-Path $repoRoot 'eng\knowledge-map\build\knowledge-map.json'
$buildScript = Join-Path $PSScriptRoot 'build-knowledge-map.ps1'

function Normalize-SearchText {
  param(
    [string]$Text
  )

  if ([string]::IsNullOrWhiteSpace($Text)) {
    return ''
  }

  $value = $Text.ToLowerInvariant()
  $value = $value -replace '[^\p{L}\p{Nd}-]+', ' '
  $value = $value -replace '\s+', ' '
  return $value.Trim()
}

if (-not (Test-Path $mapPath)) {
  & powershell -ExecutionPolicy Bypass -File $buildScript
  if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
  }
}

$map = Get-Content -Raw -Encoding UTF8 $mapPath | ConvertFrom-Json
$queryNorm = Normalize-SearchText $Query

$results = foreach ($record in $map.searchRecords) {
  $score = 0
  $matchReason = $null

  foreach ($term in $record.normalizedExactTerms) {
    if ([string]::IsNullOrWhiteSpace($term)) {
      continue
    }

    if ($term -eq $queryNorm) {
      $score = [Math]::Max($score, 3000 + [int]$record.authority)
      $matchReason = 'exact'
      continue
    }

    if ($term.StartsWith($queryNorm) -or $queryNorm.StartsWith($term)) {
      $score = [Math]::Max($score, 2200 + [int]$record.authority)
      $matchReason = 'prefix'
    }
  }

  if ($score -eq 0) {
    foreach ($keyword in $record.normalizedKeywords) {
      if ($keyword -eq $queryNorm) {
        $score = 1600 + [int]$record.authority
        $matchReason = 'keyword'
        break
      }
    }
  }

  if ($score -eq 0 -and $record.normalizedSearchText.Contains($queryNorm)) {
    $score = 1000 + [int]$record.authority
    $matchReason = 'contains'
  }

  if ($score -gt 0) {
    [PSCustomObject]@{
      Id = $record.id
      RecordType = $record.recordType
      Kind = $record.kind
      DisplayName = $record.displayName
      Path = $record.path
      Authority = [int]$record.authority
      Match = $matchReason
      Score = $score
    }
  }
}

$topResults = $results |
  Sort-Object -Property @{ Expression = 'Score'; Descending = $true }, @{ Expression = 'Authority'; Descending = $true }, @{ Expression = 'Id'; Descending = $false } |
  Select-Object -First $Top

if (-not $topResults) {
  Write-Output "No matches for '$Query'."
  exit 0
}

foreach ($result in $topResults) {
  Write-Output "[$($result.RecordType)] $($result.Id)"
  Write-Output "name: $($result.DisplayName)"
  Write-Output "kind: $($result.Kind)"
  if (-not [string]::IsNullOrWhiteSpace($result.Path)) {
    Write-Output "path: $($result.Path)"
  }
  Write-Output "match: $($result.Match)"
  Write-Output "score: $($result.Score)"
  Write-Output ''
}
