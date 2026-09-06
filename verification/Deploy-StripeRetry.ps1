param([ValidateSet('Stage','Live')][string]$Target = 'Stage')
$ErrorActionPreference = 'Stop'
$siteName = if ($Target -eq 'Stage') { 'hood-localization-staging-20260812' } else { 'hoodarcheryshop.com' }
$siteRoot = if ($Target -eq 'Stage') { 'D:\hood-localization-stage-final-20260813' } else { 'D:\hood-localization-live-20260821-0900-bnpl-stripe-country-fix' }
$appcmd = "$env:windir\System32\inetsrv\appcmd.exe"
$actual = (& $appcmd list vdir "$siteName/" /text:physicalPath).Trim()
if ($actual -ne $siteRoot) { throw 'IIS path changed; deployment aborted.' }
$targetDll = Join-Path $siteRoot 'Plugins\Nop.Plugin.Payments.Stripe\Nop.Plugin.Payments.Stripe.dll'
$candidate = Join-Path $PSScriptRoot 'bin\Release\net9.0\Nop.Plugin.Payments.Stripe.dll'
$expectedBefore = '5BAD6EA050B799446868AD2FCDA0E2FBDE107561384CB0721E076668CE29EE5C'
$expectedAfter = 'EBA7EAD5E507D1AFD2706CFE9CE54C805870A5EE82BC1352ABE4C95C0FF65058'
if ((Get-FileHash -LiteralPath $targetDll).Hash -ne $expectedBefore) { throw 'Live/stage artifact drift; deployment aborted.' }
if ((Get-FileHash -LiteralPath $candidate).Hash -ne $expectedAfter) { throw 'Candidate differs from tested artifact.' }
$rollbackDirectory = Join-Path $PSScriptRoot "rollback\$Target"
New-Item -ItemType Directory -Path $rollbackDirectory -Force | Out-Null
$backup = Join-Path $rollbackDirectory 'Nop.Plugin.Payments.Stripe.dll'
if (Test-Path -LiteralPath $backup) { throw 'Rollback file already exists; refusing overwrite.' }
Copy-Item -LiteralPath $targetDll -Destination $backup
& $appcmd stop apppool "/apppool.name:$siteName"
if ($LASTEXITCODE -ne 0) { throw 'Cannot stop target application pool.' }
try {
    Copy-Item -LiteralPath $candidate -Destination $targetDll -Force
    if ((Get-FileHash -LiteralPath $targetDll).Hash -ne $expectedAfter) { throw 'Deployed hash mismatch.' }
} catch {
    Copy-Item -LiteralPath $backup -Destination $targetDll -Force
    throw
} finally {
    & $appcmd start apppool "/apppool.name:$siteName"
}
Get-FileHash -LiteralPath $targetDll
Write-Output "Rollback: stop only $siteName pool, restore $backup to $targetDll, start the same pool. No settings or orders changed."
