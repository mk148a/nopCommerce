
param(
  [Parameter(Mandatory=$true)]
  [string]$SiteRoot,

  [switch]$SkipRecycle
)

$ErrorActionPreference = "Stop"

function Normalize-Path([string]$p) { return [System.IO.Path]::GetFullPath($p) }

$SiteRoot = Normalize-Path $SiteRoot
if (!(Test-Path $SiteRoot)) { throw "SiteRoot not found: $SiteRoot" }

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupRoot = Join-Path $SiteRoot "_hood_cleanup_backup_legacy_review_zoom_$stamp"
New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null

$headFiles = @(
  (Join-Path $SiteRoot "Themes\Element\Views\Shared\_Root.Head.cshtml"),
  (Join-Path $SiteRoot "Views\Shared\_Root.Head.cshtml"),
  (Join-Path $SiteRoot "Themes\Element\Views\Shared\_Root.cshtml"),
  (Join-Path $SiteRoot "Views\Shared\_Root.cshtml")
)

$patterns = @(
  "hood-v10-4-review-image-hardfix",
  "hood-v10-5-review-layout-fix",
  "hood-v10-6-review-hover-zoom-restore",
  "hood-v10-7-review-zoom-delegated",
  "hood-review-zoom-v107"
)

foreach ($file in $headFiles) {
  if (!(Test-Path $file)) { continue }

  $txt = Get-Content $file -Raw
  $old = $txt

  foreach ($p in $patterns) {
    $txt = [regex]::Replace($txt, "(?im)^\s*.*" + [regex]::Escape($p) + ".*\r?\n?", "")
  }

  if ($txt -ne $old) {
    Copy-Item $file (Join-Path $backupRoot ((Split-Path $file -Leaf) + ".bak")) -Force
    Set-Content -Path $file -Value $txt -Encoding UTF8
    Write-Host "Removed legacy review zoom references from: $file" -ForegroundColor Green
  } else {
    Write-Host "No legacy review zoom reference found in: $file" -ForegroundColor DarkGray
  }
}

$legacyFiles = @(
  "Themes\Element\Content\css\hood-v10-4-review-image-hardfix.css",
  "Themes\Element\Content\scripts\hood-v10-4-review-image-hardfix.js",
  "Themes\Element\Content\css\hood-v10-5-review-layout-fix.css",
  "Themes\Element\Content\scripts\hood-v10-5-review-layout-fix.js",
  "Themes\Element\Content\css\hood-v10-6-review-hover-zoom-restore.css",
  "Themes\Element\Content\scripts\hood-v10-6-review-hover-zoom-restore.js",
  "Themes\Element\Content\css\hood-v10-7-review-zoom-delegated.css",
  "Themes\Element\Content\scripts\hood-v10-7-review-zoom-delegated.js"
)

foreach ($rel in $legacyFiles) {
  $path = Join-Path $SiteRoot $rel
  if (Test-Path $path) {
    $bak = Join-Path $backupRoot ($rel -replace '[\\/:*?"<>|]', '_')
    Copy-Item $path $bak -Force
    Remove-Item $path -Force
    Write-Host "Deleted legacy file: $path" -ForegroundColor Green
  }
}

if (!$SkipRecycle) {
  try {
    Import-Module WebAdministration -ErrorAction Stop
    $site = Get-Website | Where-Object {
      try { (Normalize-Path $_.PhysicalPath) -ieq $SiteRoot } catch { $false }
    } | Select-Object -First 1
    if ($site) {
      Restart-WebAppPool -Name $site.applicationPool
      Write-Host "Recycled App Pool: $($site.applicationPool)" -ForegroundColor Green
    } else {
      $webConfig = Join-Path $SiteRoot "web.config"
      if (Test-Path $webConfig) { (Get-Item $webConfig).LastWriteTime = Get-Date; Write-Host "Touched web.config." -ForegroundColor Yellow }
    }
  } catch {
    $webConfig = Join-Path $SiteRoot "web.config"
    if (Test-Path $webConfig) { (Get-Item $webConfig).LastWriteTime = Get-Date; Write-Host "Touched web.config." -ForegroundColor Yellow }
  }
}

Write-Host ""
Write-Host "Done. Backup: $backupRoot" -ForegroundColor Cyan
Write-Host "Clear nopCommerce cache and CDN/Cloudflare cache." -ForegroundColor Cyan
