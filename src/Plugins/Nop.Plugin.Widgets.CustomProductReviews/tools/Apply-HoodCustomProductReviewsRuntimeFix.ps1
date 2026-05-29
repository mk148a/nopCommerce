param(
  [Parameter(Mandatory=$true)]
  [string]$SiteRoot,

  [string]$ConnectionString = "",
  [switch]$SkipCopyFiles,
  [switch]$SkipSql,
  [switch]$SkipRecycle,
  [switch]$SkipThemeReviewFixCleanup
)

$ErrorActionPreference = "Stop"

function Normalize-Path([string]$p) { return [System.IO.Path]::GetFullPath($p) }

function Normalize-ConnectionString([string]$cs) {
  if ([string]::IsNullOrWhiteSpace($cs)) { return $cs }
  $cs = $cs -replace '(?i)Trust Server Certificate\s*=', 'TrustServerCertificate='
  $cs = $cs -replace '(?i)Encrypt Optional\s*=', 'Encrypt='
  return $cs
}

function Get-NopConnectionString([string]$root) {
  $candidates = @(
    (Join-Path $root "App_Data\appsettings.json"),
    (Join-Path $root "appsettings.json"),
    (Join-Path $root "App_Data\dataSettings.json")
  )
  foreach ($file in $candidates) {
    if (!(Test-Path $file)) { continue }
    try {
      $json = Get-Content $file -Raw | ConvertFrom-Json
      if ($json.ConnectionStrings) {
        foreach ($p in $json.ConnectionStrings.PSObject.Properties) {
          $v = [string]$p.Value
          if ($v -match "Server=|Data Source=|Initial Catalog=|Database=") {
            Write-Host "Using SQL connection from: $file / ConnectionStrings.$($p.Name)" -ForegroundColor Green
            return (Normalize-ConnectionString $v)
          }
        }
      }
      foreach ($name in @("DataConnectionString", "ConnectionString")) {
        if ($json.PSObject.Properties.Name -contains $name) {
          $v = [string]$json.$name
          if ($v -match "Server=|Data Source=|Initial Catalog=|Database=") {
            Write-Host "Using SQL connection from: $file / $name" -ForegroundColor Green
            return (Normalize-ConnectionString $v)
          }
        }
      }
    } catch { Write-Warning "Could not parse $file : $($_.Exception.Message)" }
  }
  return ""
}

function Invoke-SqlNonQuery([string]$cs, [string]$sql) {
  $conn = New-Object System.Data.SqlClient.SqlConnection (Normalize-ConnectionString $cs)
  $cmd = $conn.CreateCommand()
  $cmd.CommandTimeout = 300
  $cmd.CommandText = $sql
  $conn.Open()
  try { return $cmd.ExecuteNonQuery() } finally { $conn.Close() }
}

function Invoke-SqlReaderTable([string]$cs, [string]$sql) {
  $conn = New-Object System.Data.SqlClient.SqlConnection (Normalize-ConnectionString $cs)
  $cmd = $conn.CreateCommand()
  $cmd.CommandTimeout = 300
  $cmd.CommandText = $sql
  $dt = New-Object System.Data.DataTable
  $conn.Open()
  try {
    $reader = $cmd.ExecuteReader()
    $dt.Load($reader)
    return $dt
  } finally { $conn.Close() }
}

function Cleanup-OldThemeReviewFixes([string]$siteRoot, [string]$backupRoot) {
  $headFiles = @(
    (Join-Path $siteRoot "Themes\Element\Views\Shared\_Root.Head.cshtml"),
    (Join-Path $siteRoot "Views\Shared\_Root.Head.cshtml")
  )
  $patterns = @(
    "hood-v10-4-review-image-hardfix",
    "hood-v10-5-review-layout-fix",
    "hood-v10-6-review-hover-zoom-restore",
    "hood-v10-7-review-zoom-delegated"
  )

  foreach ($file in $headFiles) {
    if (!(Test-Path $file)) { continue }
    $txt = Get-Content $file -Raw
    $old = $txt
    foreach ($p in $patterns) {
      $txt = [regex]::Replace($txt, "(?im)^\s*.*" + [regex]::Escape($p) + ".*\r?\n?", "")
    }
    if ($txt -ne $old) {
      Copy-Item $file (Join-Path $backupRoot ((Split-Path $file -Leaf) + ".before-review-cleanup.bak")) -Force
      Set-Content -Path $file -Value $txt -Encoding UTF8
      Write-Host "Cleaned old theme review fix includes from: $file" -ForegroundColor Green
    }
  }
}

$SiteRoot = Normalize-Path $SiteRoot
if (!(Test-Path $SiteRoot)) { throw "SiteRoot not found: $SiteRoot" }

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupRoot = Join-Path $SiteRoot "_hood_patch_backup_customproductreviews_108_$stamp"
New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null

$packageRoot = Split-Path -Parent $PSScriptRoot
$pluginDest = Join-Path $SiteRoot "Plugins\Widgets.CustomProductReviews"
if (!(Test-Path $pluginDest)) {
  Write-Warning "Live plugin folder not found: $pluginDest"
  Write-Warning "File copy skipped. SQL resources can still be applied."
  $SkipCopyFiles = $true
}

if (!$SkipCopyFiles) {
  $copyList = @(
    "Content\style.css",
    "Content\style.min.css",
    "Content\review-media.css",
    "Content\js\review-media.js",
    "Views\_ProductReviewPictures.cshtml",
    "Views\CustomProductReviews\_ProductReviewPictures.cshtml",
    "Views\_ProductReviewVideos.cshtml",
    "Views\Product\_ProductReviews.cshtml",
    "Views\ProductReviewComponent.cshtml",
    "Themes\Element\Views\Product\_ProductReviews.cshtml",
    "Resources\customproductreviews.resources.v108.json",
    "Resources\upsert-customproductreviews-resources-v108.sql"
  )

  foreach ($rel in $copyList) {
    $src = Join-Path $packageRoot $rel
    $dst = Join-Path $pluginDest $rel
    if (!(Test-Path $src)) { Write-Warning "Missing package file: $src"; continue }
    New-Item -ItemType Directory -Path (Split-Path $dst) -Force | Out-Null
    if (Test-Path $dst) {
      $bak = Join-Path $backupRoot ($rel -replace '[\\/:*?"<>|]', '_')
      Copy-Item $dst $bak -Force
    }
    Copy-Item $src $dst -Force
    Write-Host "Copied: $dst" -ForegroundColor Green
  }
}

if (!$SkipThemeReviewFixCleanup) {
  Cleanup-OldThemeReviewFixes $SiteRoot $backupRoot
}

if (!$SkipSql) {
  if (!$ConnectionString) { $ConnectionString = Get-NopConnectionString $SiteRoot }
  $ConnectionString = Normalize-ConnectionString $ConnectionString
  if (!$ConnectionString) { throw "SQL connection string not found. Use -ConnectionString manually." }

  $suffix = $stamp.Replace('-', '').Replace(':', '')
  $backupSql = "IF OBJECT_ID('HoodCustomProductReviewsResourceBackup_108_$suffix','U') IS NULL SELECT GETDATE() BackupAt,* INTO HoodCustomProductReviewsResourceBackup_108_$suffix FROM LocaleStringResource WHERE LOWER(ResourceName) LIKE 'plugins.widgets.customproductreviews.%' OR LOWER(ResourceName) IN ('product reviews for','product reviews for ');"
  [void](Invoke-SqlNonQuery $ConnectionString $backupSql)

  $sqlFile = Join-Path $packageRoot "Resources\upsert-customproductreviews-resources-v108.sql"
  if (!(Test-Path $sqlFile)) { throw "SQL file not found: $sqlFile" }

  $sqlText = Get-Content $sqlFile -Raw
  $dt = Invoke-SqlReaderTable $ConnectionString $sqlText

  Write-Host "Locale resources inserted/updated from v1.08 SQL." -ForegroundColor Green
  Write-Host "Resource count per language after patch:" -ForegroundColor Cyan
  $dt | Format-Table -AutoSize | Out-String | Write-Host
  Write-Host "DB backup table: HoodCustomProductReviewsResourceBackup_108_$suffix" -ForegroundColor Cyan
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
Write-Host "Done. Backup folder: $backupRoot" -ForegroundColor Cyan
Write-Host "Clear nopCommerce cache and CDN/Cloudflare cache." -ForegroundColor Cyan
Write-Host "Console check: CPR_reviewMediaReport && CPR_reviewMediaReport()" -ForegroundColor Cyan