param(
  [Parameter(Mandatory=$true)]
  [string]$SiteRoot,

  [string]$ConnectionString = "",
  [switch]$SkipRecycle
)

$ErrorActionPreference = "Stop"

function Normalize-ConnectionString([string]$cs) {
  if ([string]::IsNullOrWhiteSpace($cs)) { return $cs }
  $cs = $cs -replace '(?i)Trust Server Certificate\s*=', 'TrustServerCertificate='
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
  }
  return ""
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

if (!$ConnectionString) { $ConnectionString = Get-NopConnectionString $SiteRoot }
if (!$ConnectionString) { throw "SQL connection string not found. Use -ConnectionString manually." }

$packageRoot = Split-Path -Parent $PSScriptRoot
$sqlFile = Join-Path $packageRoot "Resources\upsert-customproductreviews-resources-v108.sql"
if (!(Test-Path $sqlFile)) { throw "SQL file not found: $sqlFile" }

$dt = Invoke-SqlReaderTable $ConnectionString (Get-Content $sqlFile -Raw)
$dt | Format-Table -AutoSize | Out-String | Write-Host

if (!$SkipRecycle) {
  $webConfig = Join-Path $SiteRoot "web.config"
  if (Test-Path $webConfig) { (Get-Item $webConfig).LastWriteTime = Get-Date; Write-Host "Touched web.config to clear cache." -ForegroundColor Green }
}