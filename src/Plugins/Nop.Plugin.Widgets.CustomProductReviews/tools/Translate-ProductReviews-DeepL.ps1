param(
  [Parameter(Mandatory=$true)]
  [string]$SiteRoot,

  [string]$DeepLAuthKey = "",

  [switch]$UseFreeDeepL,
  [string]$ConnectionString = "",
  [int]$Limit = 0,
  [int]$DelayMs = 300,
  [switch]$Force,
  [switch]$RepairMojibake,
  [switch]$RepairTruncated,
  [string[]]$TargetLanguageCodes = @(),
  [string[]]$ReviewIds = @(),
  [ValidateRange(1, 50)]
  [int]$BatchTextCount = 25,
  [ValidateRange(1024, 65536)]
  [int]$BatchCharacterLimit = 40000,
  [switch]$DryRun,
  [switch]$OnlyMissing = $true
)

$ErrorActionPreference = "Stop"

# Windows PowerShell does not load System.Net.Http by default; PowerShell 7 already has it.
if ($null -eq ("System.Net.Http.HttpClient" -as [type])) {
  Add-Type -AssemblyName System.Net.Http
}

if ([string]::IsNullOrWhiteSpace($DeepLAuthKey)) {
  $DeepLAuthKey = $env:HOOD_DEEPL_AUTH_KEY
}

if ([string]::IsNullOrWhiteSpace($DeepLAuthKey)) {
  throw "Set HOOD_DEEPL_AUTH_KEY for this process or pass -DeepLAuthKey. Do not save the key in source control or appsettings."
}

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
    # Keep DataTable intact; PowerShell otherwise enumerates its DataRows.
    return ,$dt
  } finally {
    $conn.Close()
  }
}

function Write-LocalizedPropertiesAtomically([string]$cs, $writes) {
  if ($null -eq $writes -or $writes.Count -eq 0) { return 0 }

  $conn = New-Connection $cs
  $transaction = $null
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

  $conn.Open()
  try {
    $transaction = $conn.BeginTransaction()
    $cmd.Transaction = $transaction
    $written = 0
    foreach ($write in $writes) {
      $cmd.Parameters["@EntityId"].Value = [int]$write.ReviewId
      $cmd.Parameters["@LanguageId"].Value = [int]$write.LanguageId
      $cmd.Parameters["@LocaleKey"].Value = [string]$write.LocaleKey
      $cmd.Parameters["@LocaleValue"].Value = [string]$write.LocaleValue
      $affected = $cmd.ExecuteNonQuery()
      if ($affected -ne 1) {
        throw "Atomic localized-property write affected $affected rows; expected exactly one."
      }
      $written++
    }
    $transaction.Commit()
    return $written
  } catch {
    if ($transaction) { $transaction.Rollback() }
    throw
  } finally {
    if ($transaction) { $transaction.Dispose() }
    $cmd.Dispose()
    $conn.Close()
    $conn.Dispose()
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

function Test-Mojibake([string]$value) {
  if ([string]::IsNullOrWhiteSpace($value)) { return $false }

  # Do not flag valid language characters such as Portuguese \"Ã\".  Match only
  # byte-decoding sequences that arise when UTF-8 text is read as Windows-1252.
  $c3 = [regex]::Escape([string][char]0x00C3)
  $c2 = [regex]::Escape([string][char]0x00C2)
  $e2 = [regex]::Escape([string][char]0x00E2)
  $euro = [regex]::Escape([string][char]0x20AC)
  # Additional UTF-8-as-Windows-1252 lead bytes for Greek, Cyrillic, Arabic
  # and Japanese.  Requiring a following C1/control byte avoids treating
  # valid letters such as Danish Ø or Portuguese ã as mojibake.
  $ce = [regex]::Escape([string][char]0x00CE)
  $d0 = [regex]::Escape([string][char]0x00D0)
  $d1 = [regex]::Escape([string][char]0x00D1)
  $d8 = [regex]::Escape([string][char]0x00D8)
  $d9 = [regex]::Escape([string][char]0x00D9)
  $e3 = [regex]::Escape([string][char]0x00E3)

  return $value -match "$c3[\u00A0-\u00BF]" -or
    $value -match "$c2[\u0080-\u00BF]" -or
    $value -match "$e2(?:[\u0080-\u00BF]{2}|$euro[\u0080-\uFFFF])" -or
    $value -match "(?:$ce|$d0|$d1|$d8|$d9|$e3)[\u0080-\u00BF]"
}

function Normalize-ReviewTextForTranslation([string]$value) {
  # Stored legacy reviews can contain HTML character references. DeepL should
  # receive the reader-visible text, not entity syntax such as &#x27;.
  return [System.Net.WebUtility]::HtmlDecode($value ?? [string]::Empty)
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
  if ($source -match '(^|\s|-)ms($|\s|-)|malay|melayu') { return "MS" }
  if ($source -match '(^|\s|-)ur($|\s|-)|urdu') { return "UR" }
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
    $pairs.Add([System.Collections.Generic.KeyValuePair[string,string]]::new('target_lang', $targetLang))
    $pairs.Add([System.Collections.Generic.KeyValuePair[string,string]]::new('tag_handling', 'html'))
    $pairs.Add([System.Collections.Generic.KeyValuePair[string,string]]::new('preserve_formatting', '1'))

    foreach ($t in $texts) {
      $pairs.Add([System.Collections.Generic.KeyValuePair[string,string]]::new('text', [string]$t))
    }

    $client.DefaultRequestHeaders.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new(
      'DeepL-Auth-Key', $DeepLAuthKey)
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

function Invoke-TranslationBatch($payload, $queue, $language, [string]$cs) {
  if ($payload.Count -eq 0) { return }

  Write-Host "LanguageId $($language.LanguageId) / $($language.DeepL): $($payload.Count) fields in one DeepL request" -ForegroundColor Yellow
  $script:totalQueued += $queue.Count
  $script:totalQueuedTruncated += @($queue | Where-Object { $_.RepairTruncated }).Count
  if ($DryRun) { return }

  # ArrayList is enumerated normally for batches, but PowerShell can bind a
  # single remaining item as the collection itself.  Pass a concrete string
  # array so the final one-item batch reaches DeepL as the review text.
  # Wrap the function output explicitly.  A single DeepL result otherwise
  # arrives as a scalar string and `$translated[0]` would mean its first
  # character rather than the first translated field.
  $translated = @(Translate-DeepL -texts ([string[]]$payload.ToArray()) -targetLang ([string]$language.DeepL))
  if ($translated.Count -ne $queue.Count) {
    throw "DeepL returned $($translated.Count) translations for $($queue.Count) requested fields. No writes were made for this batch."
  }

  $script:totalApiCalls++
  for ($i = 0; $i -lt $queue.Count; $i++) {
    # With tag_handling=html DeepL can return character references such as
    # C&#x27;est. Store the reader-visible Unicode text; Razor will perform the
    # output encoding when the review is rendered.
    $value = [System.Net.WebUtility]::HtmlDecode([string]$translated[$i])
    if ([string]::IsNullOrWhiteSpace($value)) { continue }

    $entry = $queue[$i]
    if ($entry.RepairTruncated -and $value.Trim().Length -le 1) {
      throw "DeepL returned another truncated ReviewText. No localized-property writes were made."
    }
    $script:pendingWrites.Add([pscustomobject]@{
      ReviewId = [int]$entry.ReviewId
      LanguageId = [int]$entry.LanguageId
      LocaleKey = [string]$entry.LocaleKey
      LocaleValue = $value
      RepairTruncated = [bool]$entry.RepairTruncated
    })
    $exists["$($entry.ReviewId)|$($entry.LanguageId)|$($entry.LocaleKey)"] = $value
  }

  Start-Sleep -Milliseconds $DelayMs
}

if (!$ConnectionString) { $ConnectionString = Get-NopConnectionString $SiteRoot }
if (!$ConnectionString) { throw "SQL connection string not found. Use -ConnectionString manually." }

$langs = Invoke-Query $ConnectionString @"
SELECT Id, Name, LanguageCulture, UniqueSeoCode, Published
FROM [Language]
WHERE Published = 1
ORDER BY Id
"@

$backupTable = ""
if (!$DryRun) {
  $backupTable = "HoodProductReviewTranslationBackup_" + (Get-Date).ToUniversalTime().ToString("yyyyMMddHHmmss")
  [void](Invoke-Query $ConnectionString @"
IF OBJECT_ID(N'$backupTable', N'U') IS NULL
BEGIN
  SELECT SYSUTCDATETIME() AS BackupCreatedOnUtc, *
  INTO [$backupTable]
  FROM LocalizedProperty
  WHERE LocaleKeyGroup = N'ProductReview'
    AND LocaleKey IN (N'Title', N'ReviewText', N'ReplyText');
END
"@)
}

$reviewsTop = if ($Limit -gt 0) { "TOP ($Limit)" } else { "" }
$reviewIdFilter = ""
if ($ReviewIds.Count -gt 0) {
  $validatedReviewIds = [System.Collections.Generic.List[int]]::new()
  foreach ($rawReviewId in ($ReviewIds | ForEach-Object { $_ -split ',' })) {
    $parsedReviewId = 0
    if (![int]::TryParse($rawReviewId.Trim(), [ref]$parsedReviewId) -or $parsedReviewId -le 0) {
      throw "Invalid review ID '$rawReviewId'. ReviewIds must contain positive numeric IDs only."
    }
    if (!$validatedReviewIds.Contains($parsedReviewId)) {
      $validatedReviewIds.Add($parsedReviewId)
    }
  }
  $reviewIdFilter = "AND pr.Id IN (" + ($validatedReviewIds -join ',') + ")"
}
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
  $reviewIdFilter
  AND (
    NULLIF(LTRIM(RTRIM(ISNULL(pr.Title,''))), '') IS NOT NULL
    OR NULLIF(LTRIM(RTRIM(ISNULL(pr.ReviewText,''))), '') IS NOT NULL
  )
ORDER BY pr.Id DESC
"@

$reviews = Invoke-Query $ConnectionString $reviewsSql

Write-Host "Languages: $($langs.Rows.Count), reviews: $($reviews.Rows.Count)" -ForegroundColor Cyan

$langTargets = @()
foreach ($languageRow in $langs.Rows) {
  $target = Get-DeepLTargetLang $languageRow
  $row = [pscustomobject]@{
    LanguageId = [int]$languageRow.Item("Id")
    Name = Get-ColumnValue $languageRow "Name"
    Culture = Get-ColumnValue $languageRow "LanguageCulture"
    Seo = Get-ColumnValue $languageRow "UniqueSeoCode"
    DeepL = $target
  }
  $langTargets += $row
}

Write-Host "Language target map:" -ForegroundColor Cyan
$langTargets | Format-Table -AutoSize | Out-String | Write-Host

$targets = $langTargets | Where-Object { $_.DeepL -and $_.DeepL -ne "EN" }
if ($TargetLanguageCodes.Count -gt 0) {
  $requestedCodes = @($TargetLanguageCodes |
    ForEach-Object { $_ -split ',' } |
    ForEach-Object { $_.Trim().ToLowerInvariant() } |
    Where-Object { $_ })
  $targets = @($targets | Where-Object { $requestedCodes -contains $_.Seo.ToLowerInvariant() })
}
if ($targets.Count -eq 0) {
  throw "No non-English DeepL target languages detected. Check Language.UniqueSeoCode / LanguageCulture / Name values."
}

$existing = Invoke-Query $ConnectionString @"
SELECT EntityId, LanguageId, LocaleKey, LocaleValue
FROM LocalizedProperty
WHERE LocaleKeyGroup = N'ProductReview'
  AND LocaleKey IN (N'Title', N'ReviewText')
"@

$exists = @{}
foreach ($e in $existing.Rows) {
  $exists["$($e["EntityId"])|$($e["LanguageId"])|$($e["LocaleKey"])"] = Get-ColumnValue $e "LocaleValue"
}

$script:totalWrites = 0
$script:totalApiCalls = 0
$script:totalQueued = 0
$script:totalQueuedTruncated = 0
$script:totalSkippedExisting = 0
$script:totalSkippedEmpty = 0
$script:totalSkippedUnsupported = 0
$script:pendingWrites = [System.Collections.Generic.List[object]]::new()

foreach ($l in $targets) {
  $payload = [System.Collections.ArrayList]::new()
  $queue = [System.Collections.ArrayList]::new()
  $payloadCharacters = 0

  foreach ($r in $reviews.Rows) {
    $rid = [int]$r["Id"]
    $title = [string]$r["Title"]
    $text = [string]$r["ReviewText"]
    $lid = [int]$l.LanguageId
    $target = [string]$l.DeepL

    if ([string]::IsNullOrWhiteSpace($target)) {
      $script:totalSkippedUnsupported++
      continue
    }

    $titleKey = "$rid|$lid|Title"
    $textKey = "$rid|$lid|ReviewText"
    $existingText = if ($exists.ContainsKey($textKey)) { [string]$exists[$textKey] } else { [string]::Empty }
    # A one-character body cannot be a valid translation for a substantive
    # source review. Restrict this repair to ReviewText; titles can be valid
    # one-character strings in languages such as Japanese.
    $repairTruncatedText = $RepairTruncated -and
      $exists.ContainsKey($textKey) -and
      $text.Trim().Length -ge 8 -and
      $existingText.Trim().Length -eq 1
    if ($RepairTruncated) {
      # This is an intentionally narrow repair mode. Do not combine the
      # regular missing/force/mojibake selection with a data correction run.
      $needTitle = $false
      $needText = $repairTruncatedText
    } elseif ($RepairMojibake) {
      # Repair mode must never fill unrelated missing translations. Only
      # rewrite an existing value that matches the mojibake detector.
      $needTitle = $exists.ContainsKey($titleKey) -and (Test-Mojibake $exists[$titleKey])
      $needText = $exists.ContainsKey($textKey) -and (Test-Mojibake $exists[$textKey])
    } else {
      $needTitle = $Force -or (-not $exists.ContainsKey($titleKey))
      $needText  = $Force -or (-not $exists.ContainsKey($textKey))
    }

    if (!$needTitle -and !$needText) {
      $script:totalSkippedExisting += 2
      continue
    }

    foreach ($candidate in @(
      [pscustomobject]@{ LocaleKey = "Title"; Value = $title; Needed = $needTitle },
      [pscustomobject]@{ LocaleKey = "ReviewText"; Value = $text; Needed = $needText; RepairTruncated = $repairTruncatedText }
    )) {
      if (!$candidate.Needed) { continue }
      if ([string]::IsNullOrWhiteSpace([string]$candidate.Value)) {
        $script:totalSkippedEmpty++
        continue
      }

      $normalizedValue = Normalize-ReviewTextForTranslation ([string]$candidate.Value)
      $candidateLength = $normalizedValue.Length
      if ($payload.Count -gt 0 -and (
          $payload.Count -ge $BatchTextCount -or
          ($payloadCharacters + $candidateLength) -gt $BatchCharacterLimit)) {
        Invoke-TranslationBatch $payload $queue $l $ConnectionString
        $payload = [System.Collections.ArrayList]::new()
        $queue = [System.Collections.ArrayList]::new()
        $payloadCharacters = 0
      }

      [void]$payload.Add($normalizedValue)
      [void]$queue.Add([pscustomobject]@{
        ReviewId = $rid
        LanguageId = $lid
        LocaleKey = [string]$candidate.LocaleKey
        RepairTruncated = [bool]$candidate.RepairTruncated
      })
      $payloadCharacters += $candidateLength
    }
  }

  Invoke-TranslationBatch $payload $queue $l $ConnectionString
}

if (!$DryRun) {
  # No localized row is changed until every DeepL batch has completed. The
  # final database write is one transaction, so a SQL failure cannot leave a
  # partially repaired language/review matrix behind.
  $script:totalWrites = Write-LocalizedPropertiesAtomically $ConnectionString $script:pendingWrites
}

Write-Host ""
Write-Host "Done." -ForegroundColor Green
if ($backupTable) { Write-Host "LocalizedProperty backup table: $backupTable" -ForegroundColor Cyan }
Write-Host "Queued translation fields: $script:totalQueued" -ForegroundColor Green
Write-Host "Queued truncated ReviewText repairs: $script:totalQueuedTruncated" -ForegroundColor Green
Write-Host "DeepL API calls: $script:totalApiCalls" -ForegroundColor Green
Write-Host "LocalizedProperty rows inserted/updated: $script:totalWrites" -ForegroundColor Green
Write-Host "Skipped existing fields: $script:totalSkippedExisting" -ForegroundColor DarkGray
Write-Host "Skipped empty fields: $script:totalSkippedEmpty" -ForegroundColor DarkGray
Write-Host "Skipped unsupported languages: $script:totalSkippedUnsupported" -ForegroundColor DarkGray
if ($DryRun) {
  Write-Host "Dry run: no DeepL API calls or database writes were made." -ForegroundColor Cyan
}
Write-Host ""
Write-Host "Now recycle App Pool, clear nopCommerce cache and CDN cache." -ForegroundColor Cyan
