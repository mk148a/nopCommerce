$ErrorActionPreference = 'Stop'

$pluginRoot = Split-Path -Parent $PSScriptRoot
$scriptPath = Join-Path $pluginRoot 'tools\Translate-ProductReviews-DeepL.ps1'
$content = Get-Content -LiteralPath $scriptPath -Raw

function Assert-True([bool]$Condition, [string]$Message) {
  if (-not $Condition) {
    throw $Message
  }
}

Assert-True ($content -match '\[switch\]\$RepairTruncated') 'RepairTruncated switch is missing.'
Assert-True ($content -match '\$text\.Trim\(\)\.Length\s+-ge\s+8') 'Truncated repair must require a source ReviewText of at least eight trimmed characters.'
Assert-True ($content -match '\$existingText\.Trim\(\)\.Length\s+-eq\s+1') 'Truncated repair must require an existing localized ReviewText of exactly one trimmed character.'
Assert-True ($content -match '\$exists\.ContainsKey\(\$textKey\)') 'Truncated repair must apply only to an existing localized ReviewText.'
Assert-True ($content -match 'if \(\$RepairTruncated\) \{\s*# This is an intentionally narrow repair mode\.[\s\S]*?\$needTitle\s*=\s*\$false\s*\r?\n\s*\$needText\s*=\s*\$repairTruncatedText') 'RepairTruncated must queue only eligible ReviewText values and must not queue Title.'
Assert-True ($content -match '\$script:totalQueued\s*\+=\s*\$queue\.Count\s*\r?\n\s*\$script:totalQueuedTruncated') 'Queued counts must be recorded before DryRun returns.'
Assert-True ($content -match 'if \(\$DryRun\) \{ return \}') 'DryRun must return before DeepL requests and writes.'
Assert-True ($content -match 'Dry run: no DeepL API calls or database writes were made\.') 'DryRun completion message is missing.'
Assert-True ($content -match '\$value\s*=\s*\[System\.Net\.WebUtility\]::HtmlDecode\(\[string\]\$translated\[\$i\]\)') 'DeepL output must be HTML-decoded before it is stored.'
Assert-True ($content -match '\$script:pendingWrites\.Add\(') 'DeepL batches must collect pending writes before touching the database.'
Assert-True ($content -match 'Write-LocalizedPropertiesAtomically\s+\$ConnectionString\s+\$script:pendingWrites') 'All pending translations must be committed through the atomic writer.'
Assert-True ($content -match '\$transaction\.Rollback\(\)') 'The atomic writer must roll back on a database failure.'
Assert-True ($content -match '\$entry\.RepairTruncated\s+-and\s+\$value\.Trim\(\)\.Length\s+-le\s+1') 'A truncated DeepL result must fail before any database write.'
Assert-True ($content -match '\$affected\s+-ne\s+1') 'Each atomic MERGE must prove exactly one affected row.'

Write-Host 'ReviewTranslationRepair.Static.Tests passed.' -ForegroundColor Green
