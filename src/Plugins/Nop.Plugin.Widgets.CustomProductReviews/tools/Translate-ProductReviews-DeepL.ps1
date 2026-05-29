param(
  [Parameter(Mandatory=$true)]
  [string]$SiteRoot,

  [Parameter(Mandatory=$true)]
  [string]$DeepLAuthKey,

  [switch]$UseFreeDeepL,
  [string]$ConnectionString = "",
  [int]$Limit = 0,
  [int]$DelayMs = 300,
  [switch]$Force,
  [switch]$DryRun,
  [switch]$OnlyMissing = $true
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
    } catch {
      Write-Warning "Could not parse $file : $($_.Exception.Message)"
    }
  }

  return ""
}

function New-Connection([string]$cs) {
  return New-Object System.Data.SqlClient.SqlConnection (Normalize-ConnectionString $cs)
}

function Invoke-Query([string]$cs, [string]$sql) {
  $conn = New-Connection $cs
  $cmd = $conn.CreateCommand()
  $cmd.CommandTimeout = 300
  $cmd.CommandText = $sql
  $dt = New-Object System.Data.DataTable
  $conn.Open()
  try {
    $reader = $cmd.ExecuteReader()
    $dt.Load($reader)
    return $dt
  } finally {
    $conn.Close()
  }
}

function Upsert-LocalizedProperty([string]$cs, [int]$entityId, [int]$languageId, [string]$localeKey, [string]$localeValue) {
  if ($null -eq $localeValue) { $localeValue = "" }

  $conn = New-Connection $cs
  $cmd = $conn.CreateCommand()
  $cmd.CommandTimeout = 300
  $cmd.CommandText = @"
MERGE LocalizedProperty AS T
USING (
  SELECT
    @EntityId AS EntityId,
    @LanguageId AS LanguageId,
    N'ProductReview' AS LocaleKeyGroup,
    @LocaleKey AS LocaleKey
) AS S
ON T.EntityId = S.EntityId
AND T.LanguageId = S.LanguageId
AND T.LocaleKeyGroup = S.LocaleKeyGroup
AND T.LocaleKey = S.LocaleKey
WHEN MATCHED THEN
  UPDATE SET LocaleValue = @LocaleValue
WHEN NOT MATCHED THEN
  INSERT(EntityId, LanguageId, LocaleKeyGroup, LocaleKey, LocaleValue)
  VALUES(@EntityId, @LanguageId, N'ProductReview', @LocaleKey, @LocaleValue);
"@

  [void]$cmd.Parameters.Add("@EntityId", [System.Data.SqlDbType]::Int)
  [void]$cmd.Parameters.Add("@LanguageId", [System.Data.SqlDbType]::Int)
  [void]$cmd.Parameters.Add("@LocaleKey", [System.Data.SqlDbType]::NVarChar, 400)
  [void]$cmd.Parameters.Add("@LocaleValue", [System.Data.SqlDbType]::NVarChar, -1)

  $cmd.Parameters["@EntityId"].Value = $entityId
  $cmd.Parameters["@LanguageId"].Value = $languageId
  $cmd.Parameters["@LocaleKey"].Value = $localeKey
  $cmd.Parameters["@LocaleValue"].Value = $localeValue

  $conn.Open()
  try {
    return $cmd.ExecuteNonQuery()
  } finally {
    $conn.Close()
  }
}

function Get-ColumnValue($row, [string]$name) {
  if ($null -eq $row) { return "" }
  if ($row.Table -and $row.Table.Columns.Contains($name)) {
    $v = $row[$name]
    if ($null -eq $v -or $v -is [System.DBNull]) { return "" }
    return [string]$v
  }
  return ""
}

function Get-DeepLTargetLang($row) {
  $seo = (Get-ColumnValue $row "UniqueSeoCode").Trim().ToLowerInvariant()
  $culture = (Get-ColumnValue $row "LanguageCulture").Trim().ToLowerInvariant()
  $name = (Get-ColumnValue $row "Name").Trim().ToLowerInvariant()

  $source = "$seo $culture $name"

  if ($source -match '(^|\s|-)en($|\s|-)|english|ingiliz') { return "EN" }

  if ($source -match '(^|\s|-)tr($|\s|-)|turkish|türk|turk') { return "TR" }
  if ($source -match '(^|\s|-)de($|\s|-)|german|deutsch|almanca') { return "DE" }
  if ($source -match '(^|\s|-)fr($|\s|-)|french|fran|français') { return "FR" }
  if ($source -match '(^|\s|-)es($|\s|-)|spanish|espa') { return "ES" }
  if ($source -match '(^|\s|-)it($|\s|-)|italian|italiano') { return "IT" }
  if ($source -match '(^|\s|-)nl($|\s|-)|dutch|neder') { return "NL" }
  if ($source -match '(^|\s|-)ru($|\s|-)|russian|рус') { return "RU" }
  if ($source -match '(^|\s|-)ar($|\s|-)|arab') { return "AR" }
  if ($source -match '(^|\s|-)pt($|\s|-)|portugu') { return "PT-PT" }
  if ($source -match '(^|\s|-)pl($|\s|-)|polish|polski') { return "PL" }
  if ($source -match '(^|\s|-)zh($|\s|-)|chinese|中文') { return "ZH" }
  if ($source -match '(^|\s|-)ja($|\s|-)|japanese|日本') { return "JA" }
  if ($source -match '(^|\s|-)ko($|\s|-)|korean|한국') { return "KO" }
  if ($source -match '(^|\s|-)sv($|\s|-)|(^|\s|-)se($|\s|-)|swedish|svenska') { return "SV" }
  if ($source -match '(^|\s|-)da($|\s|-)|(^|\s|-)dk($|\s|-)|danish|dansk') { return "DA" }
  if ($source -match '(^|\s|-)no($|\s|-)|(^|\s|-)nb($|\s|-)|norwegian|norsk') { return "NB" }
  if ($source -match '(^|\s|-)uk($|\s|-)|(^|\s|-)ua($|\s|-)|ukrainian|укра') { return "UK" }
  if ($source -match '(^|\s|-)ro($|\s|-)|romanian|română') { return "RO" }
  if ($source -match '(^|\s|-)bg($|\s|-)|bulgarian|българ') { return "BG" }
  if ($source -match '(^|\s|-)el($|\s|-)|(^|\s|-)gr($|\s|-)|greek|ελλην') { return "EL" }
  if ($source -match '(^|\s|-)cs($|\s|-)|czech|česk') { return "CS" }
  if ($source -match '(^|\s|-)fi($|\s|-)|finnish|suomi') { return "FI" }
  if ($source -match '(^|\s|-)hu($|\s|-)|hungarian|magyar') { return "HU" }
  if ($source -match '(^|\s|-)id($|\s|-)|indonesian|bahasa') { return "ID" }

  return ""
}

function Translate-DeepL([string[]]$texts, [string]$targetLang) {
  if ($texts.Count -eq 0) { return @() }

  $url = if ($UseFreeDeepL) {
    'https://api-free.deepl.com/v2/translate'
  } else {
    'https://api.deepl.com/v2/translate'
  }

  $client = [System.Net.Http.HttpClient]::new()
  try {
    $pairs = New-Object 'System.Collections.Generic.List[System.Collections.Generic.KeyValuePair[string,string]]'
    $pairs.Add([System.Collections.Generic.KeyValuePair[string,string]]::new('auth_key', $DeepLAuthKey))
    $pairs.Add([System.Collections.Generic.KeyValuePair[string,string]]::new('target_lang', $targetLang))
    $pairs.Add([System.Collections.Generic.KeyValuePair[string,string]]::new('tag_handling', 'html'))
    $pairs.Add([System.Collections.Generic.KeyValuePair[string,string]]::new('preserve_formatting', '1'))

    foreach ($t in $texts) {
      $pairs.Add([System.Collections.Generic.KeyValuePair[string,string]]::new('text', [string]$t))
    }

    $content = [System.Net.Http.FormUrlEncodedContent]::new($pairs)
    $response = $client.PostAsync($url, $content).GetAwaiter().GetResult()
    $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

    if (!$response.IsSuccessStatusCode) {
      throw "DeepL HTTP $([int]$response.StatusCode): $body"
    }

    $json = $body | ConvertFrom-Json
    return @($json.translations | ForEach-Object { $_.text })
  } finally {
    $client.Dispose()
  }
}

if (!$ConnectionString) { $ConnectionString = Get-NopConnectionString $SiteRoot }
if (!$ConnectionString) { throw "SQL connection string not found. Use -ConnectionString manually." }

$langs = Invoke-Query $ConnectionString @"
SELECT Id, Name, LanguageCulture, UniqueSeoCode, Published
FROM [Language]
WHERE Published = 1
ORDER BY Id
"@

$reviewsTop = if ($Limit -gt 0) { "TOP ($Limit)" } else { "" }
$reviewsSql = @"
SELECT $reviewsTop
  pr.Id,
  ISNULL(pr.Title,'') AS Title,
  ISNULL(pr.ReviewText,'') AS ReviewText,
  pr.ProductId,
  ISNULL(p.Name,'') AS ProductName
FROM ProductReview pr
INNER JOIN Product p ON p.Id = pr.ProductId
WHERE pr.IsApproved = 1
  AND p.Deleted = 0
  AND (
    NULLIF(LTRIM(RTRIM(ISNULL(pr.Title,''))), '') IS NOT NULL
    OR NULLIF(LTRIM(RTRIM(ISNULL(pr.ReviewText,''))), '') IS NOT NULL
  )
ORDER BY pr.Id DESC
"@

$reviews = Invoke-Query $ConnectionString $reviewsSql

Write-Host "Languages: $($langs.Rows.Count), reviews: $($reviews.Rows.Count)" -ForegroundColor Cyan

$langTargets = @()
foreach ($l in $langs.Rows) {
  $target = Get-DeepLTargetLang $l
  $row = [pscustomobject]@{
    LanguageId = [int]$l["Id"]
    Name = Get-ColumnValue $l "Name"
    Culture = Get-ColumnValue $l "LanguageCulture"
    Seo = Get-ColumnValue $l "UniqueSeoCode"
    DeepL = $target
  }
  $langTargets += $row
}

Write-Host "Language target map:" -ForegroundColor Cyan
$langTargets | Format-Table -AutoSize | Out-String | Write-Host

$targets = $langTargets | Where-Object { $_.DeepL -and $_.DeepL -ne "EN" }
if ($targets.Count -eq 0) {
  throw "No non-English DeepL target languages detected. Check Language.UniqueSeoCode / LanguageCulture / Name values."
}

$existing = Invoke-Query $ConnectionString @"
SELECT EntityId, LanguageId, LocaleKey
FROM LocalizedProperty
WHERE LocaleKeyGroup = N'ProductReview'
  AND LocaleKey IN (N'Title', N'ReviewText')
"@

$exists = @{}
foreach ($e in $existing.Rows) {
  $exists["$($e["EntityId"])|$($e["LanguageId"])|$($e["LocaleKey"])"] = $true
}

$totalWrites = 0
$totalApiCalls = 0
$totalSkippedExisting = 0
$totalSkippedEmpty = 0
$totalSkippedUnsupported = 0

foreach ($r in $reviews.Rows) {
  $rid = [int]$r["Id"]
  $title = [string]$r["Title"]
  $text = [string]$r["ReviewText"]

  foreach ($l in $targets) {
    $lid = [int]$l.LanguageId
    $target = [string]$l.DeepL

    if ([string]::IsNullOrWhiteSpace($target)) {
      $totalSkippedUnsupported++
      continue
    }

    $needTitle = $Force -or (-not $exists.ContainsKey("$rid|$lid|Title"))
    $needText  = $Force -or (-not $exists.ContainsKey("$rid|$lid|ReviewText"))

    if (!$needTitle -and !$needText) {
      $totalSkippedExisting += 2
      continue
    }

    $payload = @()
    $index = @()

    if ($needTitle -and ![string]::IsNullOrWhiteSpace($title)) {
      $payload += $title
      $index += "Title"
    } elseif ($needTitle) {
      $totalSkippedEmpty++
    }

    if ($needText -and ![string]::IsNullOrWhiteSpace($text)) {
      $payload += $text
      $index += "ReviewText"
    } elseif ($needText) {
      $totalSkippedEmpty++
    }

    if ($payload.Count -eq 0) {
      continue
    }

    Write-Host "Review #$rid -> LanguageId $lid / $target / fields: $($index -join ', ')" -ForegroundColor Yellow

    if ($DryRun) {
      continue
    }

    $translated = Translate-DeepL -texts $payload -targetLang $target
    $totalApiCalls++

    for ($i = 0; $i -lt $index.Count; $i++) {
      $value = if ($i -lt $translated.Count) { [string]$translated[$i] } else { "" }
      if ([string]::IsNullOrWhiteSpace($value)) { continue }

      [void](Upsert-LocalizedProperty $ConnectionString $rid $lid $index[$i] $value)
      $exists["$rid|$lid|$($index[$i])"] = $true
      $totalWrites++
    }

    Start-Sleep -Milliseconds $DelayMs
  }
}

Write-Host ""
Write-Host "Done." -ForegroundColor Green
Write-Host "DeepL API calls: $totalApiCalls" -ForegroundColor Green
Write-Host "LocalizedProperty rows inserted/updated: $totalWrites" -ForegroundColor Green
Write-Host "Skipped existing fields: $totalSkippedExisting" -ForegroundColor DarkGray
Write-Host "Skipped empty fields: $totalSkippedEmpty" -ForegroundColor DarkGray
Write-Host "Skipped unsupported languages: $totalSkippedUnsupported" -ForegroundColor DarkGray
Write-Host ""
Write-Host "Now recycle App Pool, clear nopCommerce cache and CDN cache." -ForegroundColor Cyan