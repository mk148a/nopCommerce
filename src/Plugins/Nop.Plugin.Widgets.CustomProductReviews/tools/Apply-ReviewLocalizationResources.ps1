param(
  [Parameter(Mandatory = $true)]
  [string]$SiteRoot,

  [string]$ConnectionString = "",

  [string]$ResourceSourcePath = (Join-Path $PSScriptRoot '..\ReviewLocalizationResources.cs'),

  [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

function Normalize-ConnectionString([string]$value) {
  return $value -replace '(?i)Trust Server Certificate\s*=', 'TrustServerCertificate='
}

function Get-NopConnectionString([string]$root) {
  $path = Join-Path $root 'App_Data\appsettings.json'
  if (!(Test-Path -LiteralPath $path)) {
    throw "nopCommerce appsettings was not found below the supplied site root."
  }

  $settings = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
  $value = [string]$settings.ConnectionStrings.ConnectionString
  if ([string]::IsNullOrWhiteSpace($value)) {
    throw 'ConnectionStrings.ConnectionString is missing.'
  }

  return Normalize-ConnectionString $value
}

function Get-ResourceProviderType([string]$sourcePath) {
  $resolved = (Resolve-Path -LiteralPath $sourcePath).Path
  Add-Type -Path $resolved
  $type = [AppDomain]::CurrentDomain.GetAssemblies() |
    ForEach-Object { $_.GetType('Nop.Plugin.Widgets.CustomProductReviews.ReviewLocalizationResources', $false) } |
    Where-Object { $_ } |
    Select-Object -First 1
  if (!$type) { throw 'ReviewLocalizationResources type could not be loaded.' }
  return $type
}

function New-Command([System.Data.SqlClient.SqlConnection]$connection, [System.Data.SqlClient.SqlTransaction]$transaction, [string]$sql) {
  $command = $connection.CreateCommand()
  $command.CommandTimeout = 300
  $command.CommandText = $sql
  if ($transaction) { $command.Transaction = $transaction }
  return $command
}

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
  $ConnectionString = Get-NopConnectionString $SiteRoot
} else {
  $ConnectionString = Normalize-ConnectionString $ConnectionString
}

$providerType = Get-ResourceProviderType $ResourceSourcePath
$getResources = $providerType.GetMethod(
  'GetResources',
  [System.Reflection.BindingFlags]'Static,NonPublic')
if (!$getResources) { throw 'ReviewLocalizationResources.GetResources was not found.' }

$connection = [System.Data.SqlClient.SqlConnection]::new($ConnectionString)
$languages = [System.Collections.Generic.List[object]]::new()
$connection.Open()
try {
  $command = New-Command $connection $null @'
SELECT Id, LanguageCulture, UniqueSeoCode
FROM [Language]
WHERE Published = 1
ORDER BY DisplayOrder, Id;
'@
  $reader = $command.ExecuteReader()
  try {
    while ($reader.Read()) {
      $languages.Add([pscustomobject]@{
        Id = $reader.GetInt32(0)
        Culture = $reader.GetString(1)
        SeoCode = $reader.GetString(2)
      })
    }
  } finally {
    $reader.Dispose()
    $command.Dispose()
  }

  $desired = [System.Collections.Generic.List[object]]::new()
  $resourceNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
  foreach ($language in $languages) {
    $resources = $getResources.Invoke($null, @([string]$language.Culture))
    foreach ($entry in $resources.GetEnumerator()) {
      $desired.Add([pscustomobject]@{
        LanguageId = [int]$language.Id
        Culture = [string]$language.Culture
        Name = [string]$entry.Key
        Value = [string]$entry.Value
      })
      [void]$resourceNames.Add([string]$entry.Key)
    }
  }

  $existing = @{}
  $read = New-Command $connection $null @'
SELECT LanguageId, ResourceName, ResourceValue
FROM LocaleStringResource;
'@
  $reader = $read.ExecuteReader()
  try {
    while ($reader.Read()) {
      $name = $reader.GetString(1)
      if ($resourceNames.Contains($name)) {
        $existing["$($reader.GetInt32(0))|$name"] = $reader.GetString(2)
      }
    }
  } finally {
    $reader.Dispose()
    $read.Dispose()
  }

  $added = 0
  $updated = 0
  $unchanged = 0
  foreach ($entry in $desired) {
    $key = "$($entry.LanguageId)|$($entry.Name)"
    if (!$existing.ContainsKey($key)) { $added++ }
    elseif ([string]$existing[$key] -cne [string]$entry.Value) { $updated++ }
    else { $unchanged++ }
  }

  Write-Host "Published languages: $($languages.Count)" -ForegroundColor Cyan
  Write-Host "Desired localized resources: $($desired.Count)" -ForegroundColor Cyan
  Write-Host "Would add: $added; update: $updated; unchanged: $unchanged" -ForegroundColor Yellow
  if ($DryRun) {
    Write-Host 'Dry run: no resource rows or backup tables were written.' -ForegroundColor Cyan
    return
  }

  $backupTable = 'HoodReviewLocaleResourceBackup_' + (Get-Date).ToUniversalTime().ToString('yyyyMMddHHmmss')
  $transaction = $connection.BeginTransaction()
  try {
    $backup = New-Command $connection $transaction @"
SELECT SYSUTCDATETIME() AS BackupCreatedOnUtc, *
INTO [$backupTable]
FROM LocaleStringResource;
"@
    [void]$backup.ExecuteNonQuery()
    $backup.Dispose()

    $upsert = New-Command $connection $transaction @'
MERGE LocaleStringResource AS target
USING (SELECT @LanguageId AS LanguageId, @ResourceName AS ResourceName) AS source
ON target.LanguageId = source.LanguageId
AND target.ResourceName = source.ResourceName
WHEN MATCHED THEN
  UPDATE SET ResourceValue = @ResourceValue
WHEN NOT MATCHED THEN
  INSERT (LanguageId, ResourceName, ResourceValue)
  VALUES (@LanguageId, @ResourceName, @ResourceValue);
'@
    [void]$upsert.Parameters.Add('@LanguageId', [System.Data.SqlDbType]::Int)
    [void]$upsert.Parameters.Add('@ResourceName', [System.Data.SqlDbType]::NVarChar, 400)
    [void]$upsert.Parameters.Add('@ResourceValue', [System.Data.SqlDbType]::NVarChar, -1)
    foreach ($entry in $desired) {
      $upsert.Parameters['@LanguageId'].Value = $entry.LanguageId
      $upsert.Parameters['@ResourceName'].Value = $entry.Name
      $upsert.Parameters['@ResourceValue'].Value = $entry.Value
      [void]$upsert.ExecuteNonQuery()
    }
    $upsert.Dispose()

    $transaction.Commit()
    Write-Host "Resource backup table: $backupTable" -ForegroundColor Green
    Write-Host "Localized resource rows applied: $($desired.Count)" -ForegroundColor Green
  } catch {
    $transaction.Rollback()
    throw
  } finally {
    $transaction.Dispose()
  }
} finally {
  $connection.Close()
  $connection.Dispose()
}
