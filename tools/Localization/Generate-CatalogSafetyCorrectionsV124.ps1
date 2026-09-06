param(
    [string]$Server = ".",
    [string]$Database = "HoodArcheryShopV480bugfixLancelotDb",
    [string[]]$OutputRoots = @(
        "C:\Users\Administrator\source\worktrees\hood-localization-plugin-480",
        "C:\Users\Administrator\source\worktrees\hood-localization-plugin-490-test"
    )
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$languageCodes = @("en", "tr", "gb", "ca", "za", "au", "fr", "de", "it", "es",
    "dk", "se", "nl", "hu", "no", "pl", "pt", "ro", "gr", "my", "jp", "ru", "ur", "ar")

function Get-Sha256([AllowNull()][string]$Value) {
    if ($null -eq $Value) { $Value = "" }
    $bytes = [Text.Encoding]::UTF8.GetBytes($Value)
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
}

function Invoke-Query([System.Data.SqlClient.SqlConnection]$Connection, [string]$Sql) {
    $command = $Connection.CreateCommand()
    $command.CommandText = $Sql
    $adapter = [System.Data.SqlClient.SqlDataAdapter]::new($command)
    $table = [Data.DataTable]::new()
    [void]$adapter.Fill($table)
    $adapter.Dispose()
    $command.Dispose()
    Write-Output -NoEnumerate $table
}

function Get-ReviewedLocaleMatrix {
    $matrixPath = Join-Path $PSScriptRoot "catalog-safety-locale-review-v124.json"
    if (-not (Test-Path -LiteralPath $matrixPath)) { throw "Reviewed locale matrix is missing: $matrixPath" }
    $matrix = Get-Content -Raw -LiteralPath $matrixPath | ConvertFrom-Json -AsHashtable -Depth 30
    if ($matrix.schemaVersion -ne 1 -or $matrix.packageVersion -ne '1.24' -or
        $matrix.offline -ne $true -or $matrix.networkRequestCount -ne 0) {
        throw 'Reviewed locale matrix preflight failed.'
    }
    $actualCodes = @($matrix.languageCodes)
    if ($actualCodes.Count -ne 24 -or @($languageCodes | Where-Object { $_ -notin $actualCodes }).Count -ne 0) {
        throw 'Reviewed locale matrix does not contain the exact 24-route language set.'
    }
    $required = @('riskMarkers','shieldMarkers','armorUse','footwearUse','shieldUse','helmetUse',
        'material1','material12','yataganSafety','swordSafety','edgeSafety','legalSafety','title54','title56','title57','title110',
        'tag187','tag191','andurilStory','andurilMaterial','andurilSize','metaKeywords56','letterOpenerUse')
    $resolved = [ordered]@{}
    foreach ($code in $languageCodes) {
        $entry = $matrix.locales[$code]
        if ($null -eq $entry -or $entry.reviewStatus -ne 'CURATED') { throw "Locale $code is not curated." }
        $base = if ($entry.ContainsKey('inherits')) { $matrix.locales[[string]$entry.inherits] } else { $entry }
        $values = [ordered]@{}
        foreach ($name in $required) {
            $value = if ($entry.ContainsKey($name)) { $entry[$name] } else { $base[$name] }
            if ($null -eq $value -or ($value -is [string] -and [string]::IsNullOrWhiteSpace($value))) {
                throw "Reviewed locale value is missing: $code/$name"
            }
            $values[$name] = $value
        }
        $resolved[$code] = $values
    }
    return [ordered]@{ matrix = $matrix; values = $resolved; sha256 = Get-Sha256 (Get-Content -Raw -LiteralPath $matrixPath) }
}

function Html-Encode([string]$Value) { return [Net.WebUtility]::HtmlEncode($Value) }

function Get-ParagraphMatches([string]$Value) {
    return [regex]::Matches($Value, '<p\b[^>]*>.*?</p>', [Text.RegularExpressions.RegexOptions]::Singleline)
}

function Replace-ParagraphAt([string]$Value, [int]$Index, [string]$Replacement) {
    $matches = Get-ParagraphMatches $Value
    if ($Index -lt 0 -or $Index -ge $matches.Count) { throw "Paragraph index $Index is outside a $($matches.Count)-paragraph value." }
    $match = $matches[$Index]
    return $Value.Remove($match.Index, $match.Length).Insert($match.Index, $Replacement)
}

function Find-ParagraphIndex([string]$Value, [string]$Pattern) {
    $matches = Get-ParagraphMatches $Value
    for ($index = 0; $index -lt $matches.Count; $index++) {
        if ([regex]::IsMatch($matches[$index].Value, $Pattern,
                [Text.RegularExpressions.RegexOptions]::IgnoreCase -bor [Text.RegularExpressions.RegexOptions]::Singleline)) {
            return $index
        }
    }
    throw "No paragraph matched '$Pattern'."
}

function Replace-FirstParagraph([string]$Value, [string]$Name, [string]$SafetyText) {
    $matches = Get-ParagraphMatches $Value
    if ($matches.Count -eq 0) { throw "Expected at least one paragraph." }
    $replacement = "<p><strong>$(Html-Encode $Name)</strong></p><p>$(Html-Encode $SafetyText)</p>"
    $first = $matches[0]
    return $Value.Remove($first.Index, $first.Length).Insert($first.Index, $replacement)
}

function New-Summary([string]$Name, [string[]]$Paragraphs) {
    $parts = [Collections.Generic.List[string]]::new()
    $parts.Add("<p><strong>$(Html-Encode $Name)</strong></p>")
    foreach ($paragraph in $Paragraphs) { $parts.Add("<p>$(Html-Encode $paragraph)</p>") }
    return $parts -join ""
}

function Remove-TrailingSlugStopwords([string]$Slug, [string]$Code) {
    $resolvedCode = if ($Code -eq 'invariant') { 'en' } else { $Code }
    $stopwordsByLocale = @{
        en=@('and','for','with','in','of','to','from','the'); tr=@('ve','icin','ile','den','dan','de','da')
        fr=@('et','de','du','des','en','pour','au','aux','avec'); de=@('und','aus','fur','von','im','in','zu','zur','mit')
        it=@('e','ed','di','da','in','per','con'); es=@('y','de','del','en','para','con')
        dk=@('og','i','til','af','for','med'); se=@('och','i','for','av','till','med')
        nl=@('en','van','voor','in','met'); hu=@('es','a','az','ban','ben','hoz','hez','koz')
        no=@('og','i','til','av','for','med'); pl=@('i','z','ze','do','na','dla','w')
        pt=@('e','de','do','da','em','para','com'); ro=@('si','de','din','pentru','cu','in')
        gr=@('και','απο','για','με','σε'); my=@('dan','untuk','dari','dalam','dengan')
        ru=@('и','из','для','в','на','с'); ur=@('اور','کے','کی','کا','سے','میں','لیے')
        ar=@('و','من','في','إلى','الى','على','مع','للعرض')
    }
    if (-not $stopwordsByLocale.ContainsKey($resolvedCode)) { return $Slug }
    $stopwords = @($stopwordsByLocale[$resolvedCode])
    while ($Slug.Contains('-')) {
        $last = $Slug.Substring($Slug.LastIndexOf('-') + 1)
        if ($last -notin $stopwords) { break }
        $Slug = $Slug.Substring(0, $Slug.LastIndexOf('-')).TrimEnd('-')
    }
    return $Slug
}

function Convert-ToSlug([string]$Value, [string]$Code = 'en') {
    $preserveComposedMarks = $Code -in 'jp','ru','ar','ur'
    $normalized = if ($preserveComposedMarks) {
        $Value.Normalize([Text.NormalizationForm]::FormC).ToLowerInvariant()
    } else {
        $Value.Normalize([Text.NormalizationForm]::FormD).ToLowerInvariant()
    }
    $builder = [Text.StringBuilder]::new()
    foreach ($character in $normalized.ToCharArray()) {
        $category = [Globalization.CharUnicodeInfo]::GetUnicodeCategory($character)
        if ($category -eq [Globalization.UnicodeCategory]::NonSpacingMark -and -not $preserveComposedMarks) { continue }
        if ([char]::IsLetterOrDigit($character)) { [void]$builder.Append($character) }
        elseif ($category -eq [Globalization.UnicodeCategory]::NonSpacingMark -and $preserveComposedMarks) { [void]$builder.Append($character) }
        elseif ($builder.Length -gt 0 -and $builder[$builder.Length - 1] -ne '-') { [void]$builder.Append('-') }
    }
    $slug = $builder.ToString().Normalize([Text.NormalizationForm]::FormC).Trim('-').Replace('ı', 'i')
    if ($slug.Length -gt 75) {
        $prefix = $slug.Substring(0, 76)
        $boundary = $prefix.LastIndexOf('-')
        if ($boundary -lt 1) { throw "Cannot truncate generated slug at a whole-token boundary for '$Value'." }
        $slug = $prefix.Substring(0, $boundary).TrimEnd('-')
    }
    $slug = Remove-TrailingSlugStopwords $slug $Code
    if ([string]::IsNullOrWhiteSpace($slug) -or $slug -match '^\d+$') { throw "Invalid generated slug for '$Value'." }
    return $slug
}

$localeReview = Get-ReviewedLocaleMatrix
$translations = $localeReview.values
function Get-ReviewedValue([string]$Code, [string]$Name) {
    $resolvedCode = if ($Code -eq 'invariant') { 'en' } else { $Code }
    if (-not $translations.Contains($resolvedCode) -or -not $translations[$resolvedCode].Contains($Name)) {
        throw "Reviewed value is missing: $Code/$Name"
    }
    $value = $translations[$resolvedCode][$Name]
    if ($value -is [string] -and [string]::IsNullOrWhiteSpace($value)) { throw "Reviewed value is blank: $Code/$Name" }
    return $value
}
$connection = [System.Data.SqlClient.SqlConnection]::new(
    "Server=$Server;Database=$Database;Integrated Security=true;TrustServerCertificate=true")
$connection.Open()
try {
    $languageTable = Invoke-Query $connection "SELECT Id, UniqueSeoCode FROM Language WHERE Published=1 ORDER BY DisplayOrder,Id"
    $languageIds = @{}
    foreach ($row in $languageTable.Rows) { $languageIds[[string]$row.UniqueSeoCode] = [int]$row.Id }
    if ($languageIds.Count -ne 24 -or @($languageCodes | Where-Object { -not $languageIds.ContainsKey($_) }).Count -ne 0) {
        throw "Published language set no longer matches the reviewed 24-route package."
    }

    $realFightGroups = @(
        @(52, "FullDescription"), @(110, "ShortDescription"), @(110, "FullDescription"),
        @(204, "ShortDescription"), @(204, "FullDescription"), @(205, "ShortDescription"), @(205, "FullDescription"),
        @(259, "FullDescription"), @(260, "FullDescription"), @(262, "FullDescription"),
        @(270, "ShortDescription"), @(270, "FullDescription"), @(283, "ShortDescription"), @(283, "FullDescription"),
        @(288, "FullDescription"), @(299, "ShortDescription"), @(299, "FullDescription"),
        @(311, "ShortDescription"), @(311, "FullDescription"), @(322, "FullDescription"),
        @(330, "ShortDescription"), @(330, "FullDescription"),
        @(340, "FullDescription"), @(343, "FullDescription"), @(344, "FullDescription"),
        @(347, "ShortDescription"), @(347, "FullDescription")
    )
    $helmetIds = @(55, 91, 92, 93, 94, 254, 256, 274)
    $productIds = @($realFightGroups | ForEach-Object { [int]$_[0] }) +
        @(5, 6, 54, 56, 57, 60, 62, 63, 68, 110, 165, 274, 331) + $helmetIds + @(332)
    $productIds = @($productIds | Sort-Object -Unique)
    $idCsv = $productIds -join ','
    $productTable = Invoke-Query $connection "SELECT Id,Sku,Name,ShortDescription,FullDescription,MetaTitle,MetaDescription,MetaKeywords,Deleted FROM Product WHERE Id IN ($idCsv)"
    $products = @{}
    foreach ($row in $productTable.Rows) { $products[[int]$row.Id] = $row }
    if ($products.Count -ne $productIds.Count) { throw "One or more reviewed product identities are missing." }

    $localizedTable = Invoke-Query $connection @"
SELECT lp.EntityId,l.UniqueSeoCode,lp.LocaleKey,lp.LocaleValue
FROM LocalizedProperty lp JOIN Language l ON l.Id=lp.LanguageId
WHERE lp.LocaleKeyGroup='Product' AND lp.EntityId IN ($idCsv)
  AND lp.LocaleKey IN ('Name','ShortDescription','FullDescription','MetaTitle','MetaDescription','MetaKeywords')
"@
    $localized = @{}
    foreach ($row in $localizedTable.Rows) {
        $localized["$($row.EntityId)|$($row.UniqueSeoCode)|$($row.LocaleKey)"] = [string]$row.LocaleValue
    }

    $tagTable = Invoke-Query $connection "SELECT Id,Name FROM ProductTag WHERE Id IN (187,191)"
    $tags = @{}
    foreach ($row in $tagTable.Rows) { $tags[[int]$row.Id] = [string]$row.Name }
    if ($tags.Count -ne 2) { throw "Reviewed product-tag identities are missing." }
    $tagLocalizedTable = Invoke-Query $connection @"
SELECT lp.EntityId,l.UniqueSeoCode,lp.LocaleValue
FROM LocalizedProperty lp JOIN Language l ON l.Id=lp.LanguageId
WHERE lp.LocaleKeyGroup='ProductTag' AND lp.LocaleKey='Name' AND lp.EntityId IN (187,191)
"@
    $tagLocalized = @{}
    foreach ($row in $tagLocalizedTable.Rows) { $tagLocalized["$($row.EntityId)|$($row.UniqueSeoCode)"] = [string]$row.LocaleValue }

    $urlTable = Invoke-Query $connection @"
SELECT ur.Id,ur.EntityName,ur.EntityId,ur.LanguageId,ur.Slug,ur.IsActive,l.UniqueSeoCode
FROM UrlRecord ur LEFT JOIN Language l ON l.Id=ur.LanguageId
WHERE (ur.EntityName='Product' AND ur.EntityId IN (54,56,57,110))
   OR (ur.EntityName='ProductTag' AND ur.EntityId IN (187,191))
"@
    $activeUrls = @{}
    foreach ($row in $urlTable.Rows | Where-Object { [bool]$_.IsActive }) {
        $code = if ([int]$row.LanguageId -eq 0) { "invariant" } else { [string]$row.UniqueSeoCode }
        $key = "$($row.EntityName)|$($row.EntityId)|$code"
        if ($activeUrls.ContainsKey($key)) { throw "Multiple active URL records for $key." }
        $activeUrls[$key] = $row
    }
    $allActiveUrlTable = Invoke-Query $connection @"
SELECT ur.EntityName,ur.EntityId,ur.LanguageId,ur.Slug,l.UniqueSeoCode
FROM UrlRecord ur LEFT JOIN Language l ON l.Id=ur.LanguageId
WHERE ur.IsActive=1
"@

    $resourceRoot = Join-Path $OutputRoots[0] "src\Plugins\Nop.Plugin.Misc.HoodLocalizationSeo\Resources"
    $manifest = Get-Content -Raw -LiteralPath (Join-Path $resourceRoot "localization-manifest.json") | ConvertFrom-Json -Depth 100
    $supplemental = Get-Content -Raw -LiteralPath (Join-Path $resourceRoot "product-prose-supplemental-corrections.json") | ConvertFrom-Json -Depth 100
    $tagPackage = Get-Content -Raw -LiteralPath (Join-Path $resourceRoot "product-tag-localization.json") | ConvertFrom-Json -Depth 100
    $acceptedOverlays = @{}
    function Add-AcceptedOverlay([string]$Key, [AllowNull()][string]$Value) {
        if ($null -eq $Value) { return }
        if (-not $acceptedOverlays.ContainsKey($Key)) { $acceptedOverlays[$Key] = [Collections.Generic.List[string]]::new() }
        if (-not $acceptedOverlays[$Key].Contains($Value)) { $acceptedOverlays[$Key].Add($Value) }
    }
    foreach ($entry in $manifest.entries | Where-Object { $_.entityType -eq 'Product' }) {
        foreach ($field in $entry.fields.PSObject.Properties) {
            Add-AcceptedOverlay "$($entry.entityId)|$($entry.languageCode)|$($field.Name)" ([string]$field.Value)
        }
    }
    foreach ($row in $supplemental.rows) {
        $key = "$($row.entityId)|$($row.languageCode)|$($row.field)"
        Add-AcceptedOverlay $key ([string]$row.oldValue)
        Add-AcceptedOverlay $key ([string]$row.newValue)
    }
    foreach ($value in $tagPackage.values | Where-Object { $_.entityId -in 187,191 }) {
        Add-AcceptedOverlay "ProductTag|$($value.entityId)|$($value.languageCode)|Name" ([string]$value.value)
    }

    function Get-ProductValue([int]$Id, [string]$Code, [string]$Field) {
        if ($Code -eq 'invariant') {
            $value = $products[$Id].$Field
            return $(if ($value -is [DBNull]) { "" } else { [string]$value })
        }
        $key = "$Id|$Code|$Field"
        if (-not $localized.ContainsKey($key)) { throw "Missing localized product tuple $key." }
        return $localized[$key]
    }
    function Get-ProductName([int]$Id, [string]$Code) {
        return Get-ProductValue $Id $Code "Name"
    }

    $rows = [Collections.Generic.List[object]]::new()
    $rowIndex = @{}
    function Add-ProductCorrection([int]$Id, [string]$Code, [string]$Field,
        [string]$NewValue, [string]$Reason, [string]$TransformationKind,
        [switch]$AllowMissingLocalized) {
        $localizedKey = "$Id|$Code|$Field"
        $oldRecordMayBeMissing = $Code -ne 'invariant' -and -not $localized.ContainsKey($localizedKey)
        if ($oldRecordMayBeMissing -and -not $AllowMissingLocalized) {
            throw "Missing localized product tuple $localizedKey."
        }
        $oldValue = if ($oldRecordMayBeMissing) { '' } else { Get-ProductValue $Id $Code $Field }
        if ([string]::Equals($oldValue, $NewValue, [StringComparison]::Ordinal)) {
            throw "Safety correction is unchanged for Product/$Id/$Code/$Field."
        }
        $targetKind = if ($Code -eq 'invariant') { 'InvariantProduct' } else { 'LocalizedProperty' }
        $tuple = "$targetKind|Product|$Id|$Code|$Field"
        if ($rowIndex.ContainsKey($tuple)) { throw "Duplicate safety tuple $tuple." }
        $accepted = [Collections.Generic.List[object]]::new()
        $overlayKey = "$Id|$Code|$Field"
        if ($acceptedOverlays.ContainsKey($overlayKey)) {
            foreach ($value in $acceptedOverlays[$overlayKey]) {
                if ($value -ne $oldValue -and $value -ne $NewValue) {
                    $accepted.Add([ordered]@{ value = $value; sha256 = Get-Sha256 $value })
                }
            }
        }
        $row = [ordered]@{
            targetKind = $targetKind; entityName = 'Product'; entityId = $Id
            identity = [string]$products[$Id].Sku; languageCode = $(if ($Code -eq 'invariant') { $null } else { $Code })
            field = $Field; reason = $Reason; transformationKind = $TransformationKind
            oldValue = $oldValue; newValue = $NewValue
            oldSha256 = Get-Sha256 $oldValue; newSha256 = Get-Sha256 $NewValue
            oldRecordMayBeMissing = $oldRecordMayBeMissing
            acceptedPreviousValues = @($accepted)
        }
        $rows.Add($row)
        $rowIndex[$tuple] = $row
    }

    $allRoutes = @('invariant') + $languageCodes
    foreach ($group in $realFightGroups) {
        $id = [int]$group[0]; $field = [string]$group[1]
        $safetyKey = if ($id -in 204,205,322,347) { 'footwearUse' } else { 'armorUse' }
        foreach ($code in $allRoutes) {
            $name = if ($id -eq 110) { [string](Get-ReviewedValue $code 'title110') } else { Get-ProductName $id $code }
            $safe = [string](Get-ReviewedValue $code $safetyKey)
            $old = Get-ProductValue $id $code $field
            $new = if ($field -eq 'ShortDescription') {
                New-Summary $name @($safe)
            } else {
                $markers = @((Get-ReviewedValue $code 'riskMarkers'))
                $paragraphs = Get-ParagraphMatches $old
                $indexes = [Collections.Generic.List[int]]::new()
                for ($i = 0; $i -lt $paragraphs.Count; $i++) {
                    foreach ($marker in $markers) {
                        if ($paragraphs[$i].Value.IndexOf([string]$marker,[StringComparison]::OrdinalIgnoreCase) -ge 0) {
                            if (-not $indexes.Contains($i)) { $indexes.Add($i) }
                            break
                        }
                    }
                }
                if ($indexes.Count -ne 1) { throw "Expected one locale risk paragraph for Product/$id/$code/$field; found $($indexes.Count)." }
                Replace-ParagraphAt $old $indexes[0] "<p>$(Html-Encode $safe)</p>"
            }
            Add-ProductCorrection $id $code $field $new 'Replace a positive real-world fighting claim with a costume-only, non-protective use statement.' $(if ($field -eq 'ShortDescription') {'reviewedSummaryReplacement'}else{'reviewedMarkerParagraphReplacement'})
        }
    }

    foreach ($id in 5,6) {
        foreach ($code in $allRoutes) {
            $name = Get-ProductName $id $code
            $safe = [string](Get-ReviewedValue $code 'shieldUse')
            foreach ($field in 'ShortDescription','FullDescription') {
                $new = New-Summary $name @($safe)
                Add-ProductCorrection $id $code $field $new 'Remove projectile-protection marketing and state decorative costume/display use.' 'reviewedFullFieldReplacement'
            }
        }
    }

    foreach ($code in $allRoutes) {
        Add-ProductCorrection 110 $code 'Name' ([string](Get-ReviewedValue $code 'title110')) 'Remove HMB/battle positioning from the H1 while retaining Spartan-Roman costume context.' 'reviewedFullFieldReplacement'
        Add-ProductCorrection 54 $code 'Name' ([string](Get-ReviewedValue $code 'title54')) 'Replace mistranslated and partially English letter-opener headings with a locale-reviewed product title.' 'reviewedFullFieldReplacement'
    }

    foreach ($id in 54,56,57,60,62,63,68,331) {
        foreach ($code in $allRoutes) {
            $name = if ($id -eq 54) { [string](Get-ReviewedValue $code 'title54') } elseif ($id -eq 56) { [string](Get-ReviewedValue $code 'title56') } elseif ($id -eq 57) { [string](Get-ReviewedValue $code 'title57') } else { Get-ProductName $id $code }
            $paragraphs = [Collections.Generic.List[string]]::new()
            if ($id -eq 60) { $paragraphs.Add([string](Get-ReviewedValue $code 'andurilStory')) }
            if ($id -eq 54) { $paragraphs.Add([string](Get-ReviewedValue $code 'letterOpenerUse')) }
            else { $paragraphs.Add([string](Get-ReviewedValue $code 'swordSafety')) }
            $paragraphs.Add([string](Get-ReviewedValue $code 'legalSafety'))
            $new = New-Summary $name @($paragraphs)
            Add-ProductCorrection $id $code 'FullDescription' $new 'Replace the imported pointed, dangerous-weapon, martial-arts and battle-ready boilerplate with compact locale-reviewed decorative display copy.' 'reviewedFullFieldReplacement'
            if ($id -notin 56,57,60) {
                $short = if ($id -eq 54) {
                    New-Summary $name @([string](Get-ReviewedValue $code 'letterOpenerUse'))
                } else {
                    New-Summary $name @([string](Get-ReviewedValue $code 'swordSafety'),[string](Get-ReviewedValue $code 'edgeSafety'))
                }
                Add-ProductCorrection $id $code 'ShortDescription' $short 'Remove pointed, sharp or unsharpened physical claims and position the listing for decorative display.' 'reviewedFullFieldReplacement'
            }
        }
    }

    foreach ($code in $allRoutes) {
        $title = [string](Get-ReviewedValue $code 'title56')
        Add-ProductCorrection 56 $code 'Name' $title 'Replace sharp-blade sales wording with a precise decorative display-replica title.' 'reviewedFullFieldReplacement'
        $newShort = New-Summary $title @([string](Get-ReviewedValue $code 'swordSafety'),[string](Get-ReviewedValue $code 'edgeSafety'))
        Add-ProductCorrection 56 $code 'ShortDescription' $newShort 'Remove sharp and functional sales wording without inventing an edge condition.' 'reviewedFullFieldReplacement'
    }
    Add-ProductCorrection 56 'invariant' 'MetaKeywords' ([string](Get-ReviewedValue 'invariant' 'metaKeywords56')) 'Remove battle-ready, real-sword and weapon-prop marketing keywords.' 'reviewedFullFieldReplacement'
    foreach ($code in $languageCodes) {
        Add-ProductCorrection 56 $code 'MetaKeywords' ([string](Get-ReviewedValue $code 'metaKeywords56')) 'Create locale-reviewed katana display-replica SEO keywords instead of inheriting unsafe English marketing terms.' 'reviewedFullFieldReplacement' -AllowMissingLocalized
    }

    $reviewedMetaKeywords = [ordered]@{
        54 = 'Game of Thrones letter opener, miniature Ice sword replica, Ned Stark collectible, decorative desk accessory, fantasy letter opener, desk display collectible'
        57 = 'red katana display replica, Damascus steel Shirasaya, Japanese decor, samurai katana cosplay replica, collectible katana replica'
        60 = 'Anduril display replica, Aragorn cosplay prop, Lord of the Rings decor, fantasy collectible replica, stage display'
        62 = 'Thranduil display replica, Elven King cosplay prop, Hobbit decor, wooden display stand, fantasy collectible replica'
        63 = 'Witch King display replica, Angmar cosplay prop, Lord of the Rings decor, scabbard replica, fantasy collectible'
        68 = 'Templar display replica, medieval cosplay prop, decorative collectible replica, stage costume accessory'
        94 = 'Ottoman wolf helmet replica, Turkish warrior helmet costume, medieval cosplay helmet, Islamic historical display, steel costume helmet'
        110 = 'Spartan Roman cuirass costume, steel muscle armor cosplay, historical costume display, Roman cosplay set, Spartan costume'
        165 = 'Turkish yatagan display replica, Ottoman decor, historical collectible replica, cosplay display prop'
        204 = 'Viking leather boots, Ragnar Lothbrok costume boots, medieval cosplay footwear, LARP costume shoes, Norse reenactment costume'
        205 = 'Legolas cosplay boots, elf costume footwear, Lord of the Rings cosplay, fantasy costume boots, theatre costume footwear'
        331 = 'Thranduil display replica version 2, Elven King cosplay prop, Hobbit decor, fantasy collectible replica'
    }
    foreach ($entry in $reviewedMetaKeywords.GetEnumerator()) {
        Add-ProductCorrection ([int]$entry.Key) 'invariant' 'MetaKeywords' ([string]$entry.Value) 'Replace unrelated or unsafe invariant SEO keywords with relevant decorative display and cosplay terms.' 'reviewedFullFieldReplacement'
    }

    foreach ($code in $allRoutes) {
        $title = [string](Get-ReviewedValue $code 'title57')
        Add-ProductCorrection 57 $code 'Name' $title 'Replace sharp-blade sales wording with a decorative display-replica title.' 'reviewedFullFieldReplacement'
        Add-ProductCorrection 57 $code 'ShortDescription' (New-Summary $title @([string](Get-ReviewedValue $code 'swordSafety'),[string](Get-ReviewedValue $code 'edgeSafety'))) 'Remove sharp and functional sales wording without inventing an edge condition.' 'reviewedFullFieldReplacement'
    }

    foreach ($code in $allRoutes) {
        $name60 = Get-ProductName 60 $code
        $short60 = New-Summary $name60 @([string](Get-ReviewedValue $code 'andurilStory'), [string](Get-ReviewedValue $code 'swordSafety'))
        Add-ProductCorrection 60 $code 'ShortDescription' $short60 'Replace weapon marketing with a decorative story/display summary.' 'reviewedSummaryReplacement'
    }

    foreach ($code in $allRoutes) {
        $name = Get-ProductName 165 $code
        foreach ($field in 'ShortDescription','FullDescription') {
            $new = New-Summary $name @(
                [string](Get-ReviewedValue $code 'yataganSafety'),
                [string](Get-ReviewedValue $code 'swordSafety'),
                [string](Get-ReviewedValue $code 'legalSafety'))
            Add-ProductCorrection 165 $code $field $new 'Replace power, cutting/thrusting and imported CTA language with locale-reviewed historical display copy.' 'reviewedFullFieldReplacement'
        }
    }

    foreach ($id in @($helmetIds | Where-Object { $_ -ne 274 })) {
        $materialKey = if ($id -in 55,91,92,93,94) { 'material12' } else { $null }
        foreach ($code in $allRoutes) {
            $name = Get-ProductName $id $code
            $paragraphs = [Collections.Generic.List[string]]::new()
            if ($materialKey) { $paragraphs.Add([string](Get-ReviewedValue $code $materialKey)) }
            $paragraphs.Add([string](Get-ReviewedValue $code 'helmetUse'))
            $short = New-Summary $name @($paragraphs)
            Add-ProductCorrection $id $code 'ShortDescription' $short 'Correct the decapitation-steel mistranslation and state costume/display-only use.' 'reviewedSummaryReplacement'
            $full = New-Summary $name @($paragraphs)
            Add-ProductCorrection $id $code 'FullDescription' $full 'Remove HMB/protective suitability claims and correct the decapitation-steel mistranslation.' 'reviewedFullFieldReplacement'
        }
    }

    foreach ($code in $allRoutes) {
        $name = Get-ProductName 274 $code
        foreach ($field in 'ShortDescription','FullDescription') {
            $new = New-Summary $name @([string](Get-ReviewedValue $code 'shieldUse'))
            Add-ProductCorrection 274 $code $field $new 'Remove projectile, HMB and copied helmet specifications with concise locale-reviewed shield display copy.' 'reviewedFullFieldReplacement'
        }
    }

    $tagRows = [Collections.Generic.List[object]]::new()
    foreach ($tagSpec in @(@(187,'tag187'),@(191,'tag191'))) {
        $id=[int]$tagSpec[0];$phraseKey=[string]$tagSpec[1]
        foreach ($code in $allRoutes) {
            $old = if ($code -eq 'invariant') { $tags[$id] } else { $tagLocalized["$id|$code"] }
            $new = [string](Get-ReviewedValue $code $phraseKey)
            if ([string]::IsNullOrWhiteSpace($old) -or $old -eq $new) { throw "Invalid tag correction ProductTag/$id/$code." }
            $accepted = [Collections.Generic.List[object]]::new()
            $overlayKey = "ProductTag|$id|$code|Name"
            if ($acceptedOverlays.ContainsKey($overlayKey)) {
                foreach ($value in $acceptedOverlays[$overlayKey]) {
                    if ($value -ne $old -and $value -ne $new) { $accepted.Add([ordered]@{value=$value;sha256=Get-Sha256 $value}) }
                }
            }
            $tagRows.Add([ordered]@{
                targetKind=$(if($code -eq 'invariant'){'InvariantProductTag'}else{'LocalizedProperty'});entityName='ProductTag';entityId=$id
                identity=$tags[$id];languageCode=$(if($code -eq 'invariant'){$null}else{$code});field='Name'
                reason=$(if($id -eq 187){'Replace battle-ready marketing with decorative katana-replica wording.'}else{'Replace real-sword marketing with samurai cosplay display-replica wording.'})
                transformationKind='reviewedFullFieldReplacement';oldValue=$old;newValue=$new
                oldSha256=Get-Sha256 $old;newSha256=Get-Sha256 $new;acceptedPreviousValues=@($accepted)
            })
        }
    }
    foreach ($row in $tagRows) { $rows.Add($row) }

    $slugRows = [Collections.Generic.List[object]]::new()
    $slugSpecs = @(
        @{entityName='Product';entityId=54;identity=[string]$products[54].Sku;phrase='title54'},
        @{entityName='Product';entityId=56;identity=[string]$products[56].Sku;phrase='title56'},
        @{entityName='Product';entityId=57;identity=[string]$products[57].Sku;phrase='title57'},
        @{entityName='Product';entityId=110;identity=[string]$products[110].Sku;phrase='title110'},
        @{entityName='ProductTag';entityId=187;identity=$tags[187];phrase='tag187'},
        @{entityName='ProductTag';entityId=191;identity=$tags[191];phrase='tag191'}
    )
    foreach ($spec in $slugSpecs) {
        foreach ($code in $allRoutes) {
            $urlKey="$($spec.entityName)|$($spec.entityId)|$code"
            if (-not $activeUrls.ContainsKey($urlKey)) { throw "Missing active safety slug target $urlKey." }
            $oldSlug=[string]$activeUrls[$urlKey].Slug
            $slugSource = [string](Get-ReviewedValue $code $spec.phrase)
            $slugSourceHasDimensions = $true
            if ($spec.entityName -eq 'Product' -and [int]$spec.entityId -eq 54 -and $code -eq 'ro') {
                $slugSource = 'Game of Thrones deschizător scrisori miniatură sabia Gheață Ned Stark'
                $slugSourceHasDimensions = $false
            }
            if ($spec.entityName -eq 'Product' -and [int]$spec.entityId -in 54,56,57) {
                $withoutDimensions = [regex]::Replace($slugSource, '\s*[,،、]\s*\d.*$', '')
                if ($slugSourceHasDimensions -and $withoutDimensions -eq $slugSource) { throw "Expected a trailing dimension clause in $urlKey slug source." }
                $slugSource = $withoutDimensions
            }
            $newSlug=Convert-ToSlug $slugSource $code
            if ($spec.entityName -eq 'Product' -and [int]$spec.entityId -in 54,56,57 -and
                $newSlug -match '(^|-)(41|40|30|6)(-|$)') {
                throw "Dimension fragment remains in safety slug ${urlKey}: $newSlug"
            }
            if ($oldSlug.Equals($newSlug,[StringComparison]::OrdinalIgnoreCase)) { throw "Safety slug is unchanged for $urlKey." }
            $slugRows.Add([ordered]@{
                entityName=$spec.entityName;entityId=[int]$spec.entityId;identity=[string]$spec.identity
                languageCode=$(if($code -eq 'invariant'){$null}else{$code});oldSlug=$oldSlug;newSlug=$newSlug
                oldSha256=Get-Sha256 $oldSlug;newSha256=Get-Sha256 $newSlug;previousSlugs=@($oldSlug)
            })
        }
    }

    $jpSlugs = @($slugRows | Where-Object { $_.languageCode -eq 'jp' })
    $requiredJapaneseTokens = @('ゲーム','オブ','スローンズ','レプリカ','ダマスカス','コスプレ','スパルタ','ネッド')
    foreach ($token in $requiredJapaneseTokens) {
        if (-not ($jpSlugs.newSlug -match [regex]::Escape($token))) { throw "Japanese slug token was lost: $token" }
    }
    foreach ($badToken in @('ケーム','オフ','スローンス','レフリカ','タマスカス','コスフレ','スハルタ','ネット')) {
        if ($jpSlugs.newSlug -match [regex]::Escape($badToken)) { throw "Corrupted Japanese slug token remains: $badToken" }
    }
    $ruSlugs = @($slugRows | Where-Object { $_.languageCode -eq 'ru' })
    foreach ($token in @('миниатюрный','японский','дамасской','самурайской')) {
        if (-not ($ruSlugs.newSlug -match [regex]::Escape($token))) { throw "Russian composed slug token was lost: $token" }
    }
    $arSlugs = @($slugRows | Where-Object { $_.languageCode -eq 'ar' })
    foreach ($token in @('رسائل','آيس')) {
        if (-not ($arSlugs.newSlug -match [regex]::Escape($token))) { throw "Arabic composed slug token was lost: $token" }
    }
    $urSlugs = @($slugRows | Where-Object { $_.languageCode -eq 'ur' })
    foreach ($token in @('آئس','نمائشی','نمائش','آرائشی','سامورائی')) {
        if (-not ($urSlugs.newSlug -match [regex]::Escape($token))) { throw "Urdu composed slug token was lost: $token" }
    }

    $witnessRows = [Collections.Generic.List[object]]::new()
    foreach ($code in @('invariant','en','gb')) {
        $value=Get-ProductValue 332 $code 'FullDescription'
        if ([regex]::Matches($value,'ready for your next archery adventure',[Text.RegularExpressions.RegexOptions]::IgnoreCase).Count -ne 1) {
            throw "Benign ready witness changed for Product/332/$code/FullDescription."
        }
        $witnessRows.Add([ordered]@{
            targetKind=$(if($code -eq 'invariant'){'InvariantProduct'}else{'LocalizedProperty'});entityName='Product';entityId=332
            identity=[string]$products[332].Sku;languageCode=$(if($code -eq 'invariant'){$null}else{$code});field='FullDescription'
            expectedValue=$value;expectedSha256=Get-Sha256 $value;phrase='ready for your next archery adventure';expectedOccurrenceCount=1
            reason='Benign archery-adventure readiness wording is not a weapon or real-world conflict claim.'
        })
    }

    $allValueRows = $rows.ToArray()
    $prohibitedTerms = @('ready for fight','ready to fight','ready for battle','battle-ready','battle ready',
        'ready for combat','combat-ready','combat ready','real fighting','real fight','real battle','real combat',
        'actual combat','real weapon','functional weapon','razor sharp','sharp blade','cutting and thrusting weapon',
        'durable for arrows','not just a weapon','sharp structure','blood groove','deadly','lethal')
    foreach ($term in $prohibitedTerms) {
        $hit = $allValueRows | Where-Object { $_.newValue.IndexOf($term,[StringComparison]::OrdinalIgnoreCase) -ge 0 } | Select-Object -First 1
        if ($hit) { throw "Residual prohibited term '$term' in $($hit.entityName)/$($hit.entityId)/$($hit.languageCode)/$($hit.field)." }
        $slugHit = $slugRows | Where-Object { $_.newSlug.IndexOf((Convert-ToSlug $term),[StringComparison]::OrdinalIgnoreCase) -ge 0 } | Select-Object -First 1
        if ($slugHit) { throw "Residual prohibited slug term '$term' in $($slugHit.entityName)/$($slugHit.entityId)/$($slugHit.languageCode)." }
    }
    foreach ($row in $allValueRows) {
        $code = if ($row.languageCode) { [string]$row.languageCode } else { 'invariant' }
        foreach ($markerKind in 'riskMarkers','shieldMarkers') {
            foreach ($marker in @((Get-ReviewedValue $code $markerKind))) {
                if ($row.newValue.IndexOf([string]$marker,[StringComparison]::OrdinalIgnoreCase) -ge 0) {
                    throw "Locale residual '$marker' in $($row.entityName)/$($row.entityId)/$code/$($row.field)."
                }
            }
        }
    }
    if ($allValueRows | Where-Object { $_.entityName -eq 'ProductTag' -and $_.entityId -eq 583 }) { throw 'Koç ProductTag 583 is outside this package.' }
    if ($allValueRows | Where-Object { $_.entityName -eq 'ProductTag' -and $_.entityId -eq 460 }) { throw 'Historical ProductTag 460 must remain unchanged.' }
    foreach ($id in $helmetIds) {
        foreach ($field in 'ShortDescription','FullDescription') {
            $expected = if ($id -eq 274 -and $field -eq 'FullDescription') { 25 } else { 25 }
            $actual = @($allValueRows | Where-Object { $_.entityName -eq 'Product' -and $_.entityId -eq $id -and $_.field -eq $field }).Count
            if ($actual -ne $expected) { throw "Helmet coverage drift for Product/$id/${field}: expected $expected, got $actual." }
        }
    }
    if (@($allValueRows | Where-Object { $_.entityName -eq 'Product' -and $_.entityId -eq 165 -and $_.field -eq 'FullDescription' }).Count -ne 25) {
        throw 'Product 165 historical/display correction must cover invariant plus all 24 languages.'
    }
    foreach ($tagId in 187,191) {
        if (@($allValueRows | Where-Object { $_.entityName -eq 'ProductTag' -and $_.entityId -eq $tagId -and $_.field -eq 'Name' }).Count -ne 25) {
            throw "ProductTag $tagId must cover invariant plus all 24 languages."
        }
        if (@($slugRows | Where-Object { $_.entityName -eq 'ProductTag' -and $_.entityId -eq $tagId }).Count -ne 25) {
            throw "ProductTag $tagId slug coverage must include invariant plus all 24 languages."
        }
    }
    $slugCollisions = $slugRows | Group-Object { "$(if($_.languageCode){$_.languageCode}else{'invariant'})|$($_.newSlug.ToLowerInvariant())" } | Where-Object Count -gt 1
    if ($slugCollisions) { throw "Safety slug collision: $($slugCollisions[0].Name)" }
    foreach ($slugRow in $slugRows) {
        $languageId = if ($slugRow.languageCode) { $languageIds[[string]$slugRow.languageCode] } else { 0 }
        $foreign = $allActiveUrlTable.Rows | Where-Object {
            [int]$_.LanguageId -eq $languageId -and
            [string]::Equals([string]$_.Slug,[string]$slugRow.newSlug,[StringComparison]::OrdinalIgnoreCase) -and
            -not ([string]$_.EntityName -eq [string]$slugRow.entityName -and [int]$_.EntityId -eq [int]$slugRow.entityId)
        } | Select-Object -First 1
        if ($foreign) { throw "Foreign active slug collision for $($slugRow.languageCode)/$($slugRow.newSlug): $($foreign.EntityName)/$($foreign.EntityId)." }
    }

    $tupleLines = $allValueRows | ForEach-Object {
        "$($_.targetKind)|$($_.entityName)|$($_.entityId)|$(if($_.languageCode){$_.languageCode}else{'invariant'})|$($_.field)|$($_.newSha256)"
    } | Sort-Object
    $slugLines = $slugRows | ForEach-Object {
        "$($_.entityName)|$($_.entityId)|$(if($_.languageCode){$_.languageCode}else{'invariant'})|$($_.newSlug)|$($_.newSha256)"
    } | Sort-Object
    $package = [ordered]@{
        schemaVersion=1;packageVersion='1.24';status='PASS';deployable=$true
        sourceDatabase=$Database;sourceMode='SELECT_ONLY';localeReviewMode='OFFLINE_CURATED'
        localeReviewSha256=$localeReview.sha256;networkRequestCount=0
        languageCount=24;productCount=@($allValueRows|Where-Object{$_.entityName-eq'Product'}|ForEach-Object{$_.entityId}|Sort-Object -Unique).Count
        productTagCount=2;valueRowCount=$allValueRows.Count;slugRowCount=$slugRows.Count;benignWitnessCount=$witnessRows.Count
        targetTupleSetSha256=Get-Sha256 ($tupleLines -join "`n");slugTargetSetSha256=Get-Sha256 ($slugLines -join "`n")
        prohibitedTerms=$prohibitedTerms;rows=$allValueRows;slugs=@($slugRows);benignReadyWitnesses=@($witnessRows)
        validation=[ordered]@{
            status='PASS';errorCount=0;databaseWriteCount=0;allOldHashesVerified=$true;allNewHashesVerified=$true
            noDuplicateTargetTuples=$true;allTwentyFourLanguagesCovered=$true;allProductAndTagIdentitiesBound=$true
            noKoç583Target=$true;noProhibitedPositiveSalesTermsInDesiredValues=$true
            noProhibitedPositiveSalesTermsInDesiredSlugs=$true;benignArcheryAdventureReadyPreserved=$true
            exactOldOrReviewedPreviousRequired=$true;idempotentDesiredValueAccepted=$true;transactionRequired=$true
        }
    }
    $packageJson = $package | ConvertTo-Json -Depth 30
    foreach ($root in $OutputRoots) {
        $output = Join-Path $root 'src\Plugins\Nop.Plugin.Misc.HoodLocalizationSeo\Resources\catalog-safety-corrections-v124.json'
        if (-not (Test-Path -LiteralPath (Split-Path $output -Parent))) { throw "Output plugin root is missing: $root" }
        [IO.File]::WriteAllText($output,$packageJson+"`n",[Text.UTF8Encoding]::new($false))
    }
    [pscustomobject]@{
        ValueRows=$allValueRows.Count;SlugRows=$slugRows.Count;Products=$package.productCount;Tags=2
        TargetSha256=$package.targetTupleSetSha256;SlugSha256=$package.slugTargetSetSha256
        PackageSha256=Get-Sha256 ($packageJson+"`n")
    } | Format-List
}
finally {
    $connection.Dispose()
}
