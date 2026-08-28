$ErrorActionPreference = 'Stop'

$pluginRoot = Split-Path -Parent $PSScriptRoot
$resourcePath = Join-Path $pluginRoot 'ReviewLocalizationResources.cs'
$content = Get-Content -LiteralPath $resourcePath -Raw

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw $Message
    }
}

Assert-True ($content -match 'internal static Dictionary<string, string> GetResources\(string languageCode\)') 'GetResources signature is missing.'

$pluginOwnedKeys = @(
    'Reviews.ProductReviewsFor',
    'Plugins.Widgets.CustomProductReviews.Media.Heading',
    'Plugins.Widgets.CustomProductReviews.Media.Guidance',
    'Plugins.Widgets.CustomProductReviews.Media.Requirements',
    'Plugins.Widgets.CustomProductReviews.Media.SelectedFiles',
    'Plugins.Widgets.CustomProductReviews.Media.InvalidType',
    'Plugins.Widgets.CustomProductReviews.Media.TooMany',
    'Plugins.Widgets.CustomProductReviews.Media.TooLarge',
    'Plugins.Widgets.CustomProductReviews.Review.Submitting',
    'Plugins.Widgets.CustomProductReviews.Review.SubmitError',
    'Plugins.Widgets.CustomProductReviews.Review.VoteError'
)

foreach ($key in $pluginOwnedKeys) {
    Assert-True ($content.Contains('"' + $key + '"')) "Plugin-owned resource key is missing: $key"
}

$activeCultures = @(
    'en-us', 'tr-tr', 'en-gb', 'en-ca', 'en-za', 'en-au',
    'fr-fr', 'de-de', 'it-it', 'es-es', 'da-dk', 'sv-se', 'nl-nl',
    'hu-hu', 'nn-no', 'pl-pl', 'pt-pt', 'ro-ro', 'el-gr', 'ms-my',
    'ja-jp', 'ru-ru', 'ur-pk', 'ar', 'ar-001'
)

foreach ($culture in $activeCultures) {
    Assert-True ($content.Contains('"' + $culture + '"')) "Active culture is not normalized: $culture"
}

Assert-True ($content -match '"nn"\s*=>\s*Create\(') 'nn-NO must have its own Nynorsk plugin resources.'
Assert-True ($content.Contains('Var denne omtalen nyttig?')) 'nn-NO helpfulness correction is missing.'

$expectedCoreOverrides = @(
    '("Reviews.From", "Yazan")',
    '("Reviews.AlreadyAddedProductReviews", "Bu ürün için zaten bir yorum eklendi.")',
    '("Common.No", "Nej")',
    '("Common.No", "Nie")',
    '("Reviews.Fields.Rating", "التقييم")',
    '("Reviews.AlreadyAddedProductReviews", "Pentru acest produs a fost deja adăugată o recenzie.")',
    '("Reviews.Write", "Scrie propria recenzie")'
)

foreach ($override in $expectedCoreOverrides) {
    Assert-True ($content.Contains($override)) "Confirmed core correction is missing: $override"
}

$urduRuntimeKeys = @(
    'Reviews.From', 'Reviews.Date', 'Reviews.Helpfulness.WasHelpful?', 'Common.Yes', 'Common.No',
    'Reviews.Fields.Title', 'Reviews.Fields.ReviewText', 'Reviews.Fields.Rating',
    'Reviews.Fields.Rating.Bad', 'Reviews.Fields.Rating.NotGood',
    'Reviews.Fields.Rating.NotBadNotExcellent', 'Reviews.Fields.Rating.Good',
    'Reviews.Fields.Rating.Excellent', 'Reviews.ExistingReviews', 'Reviews.SubmitButton',
    'Reviews.Write', 'Reviews.AlreadyAddedProductReviews', 'Reviews.Reply'
)

foreach ($key in $urduRuntimeKeys) {
    Assert-True ($content -match ('\("' + [regex]::Escape($key) + '", "[^"]+"\)')) "Urdu runtime correction is missing: $key"
}

$requirementLines = [regex]::Matches($content, '"[^"\r\n]*\{0\}[^"\r\n]*\{1\}[^"\r\n]*\{2\}[^"\r\n]*"')
Assert-True ($requirementLines.Count -ge 19) 'Every non-English resource set plus English fallback must preserve {0}, {1}, and {2} in media requirements.'

Write-Host 'ReviewLocalizationResources.Static.Tests passed.' -ForegroundColor Green
