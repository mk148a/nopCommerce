$ErrorActionPreference = 'Stop'

$pluginRoot = Split-Path -Parent $PSScriptRoot
$scriptPath = Join-Path $pluginRoot 'tools\Apply-ReviewLocalizationResources.ps1'
$content = Get-Content -LiteralPath $scriptPath -Raw

function Assert-True([bool]$condition, [string]$message) {
  if (!$condition) { throw $message }
}

Assert-True ($content.Contains('[switch]$DryRun')) 'DryRun switch is missing.'
Assert-True ($content -match 'if \(\$DryRun\) \{[\s\S]*?no resource rows or backup tables were written[\s\S]*?return') 'DryRun must return before backup and upsert work.'
Assert-True ($content -match 'HoodReviewLocaleResourceBackup_') 'A timestamped rollback table is required.'
Assert-True ($content -match 'BeginTransaction\(\)') 'Resource changes must be transactional.'
Assert-True ($content -match 'MERGE LocaleStringResource') 'Parameterized resource upsert is missing.'
Assert-True ($content -match "Add\('@ResourceName', \[System\.Data\.SqlDbType\]::NVarChar, 400\)") 'Resource names must be parameterized.'
Assert-True ($content -match "Add\('@ResourceValue', \[System\.Data\.SqlDbType\]::NVarChar, -1\)") 'Resource values must be Unicode and parameterized.'
Assert-True ($content -notmatch 'Write-(Host|Output).*ConnectionString') 'Connection strings must never be printed.'

Write-Host 'Review localization apply static contracts: PASS' -ForegroundColor Green
