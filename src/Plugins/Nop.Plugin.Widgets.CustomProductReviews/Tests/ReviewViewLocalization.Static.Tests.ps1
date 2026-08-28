$ErrorActionPreference = 'Stop'

$pluginRoot = Split-Path -Parent $PSScriptRoot
$componentPath = Join-Path $pluginRoot 'Views\ProductReviewComponent.cshtml'
$helpfulnessPaths = @(
    (Join-Path $pluginRoot 'Views\_ProductReviewHelpfulness.cshtml'),
    (Join-Path $pluginRoot 'Views\CustomProductReviews\_ProductReviewHelpfulness.cshtml')
)
$pluginClassPath = Join-Path $pluginRoot 'Nop.Plugin.Widgets.CustomProductReviewsPlugin.cs'
$pluginJsonPath = Join-Path $pluginRoot 'plugin.json'

$component = Get-Content -LiteralPath $componentPath -Raw
$pluginClass = Get-Content -LiteralPath $pluginClassPath -Raw
$pluginJson = Get-Content -LiteralPath $pluginJsonPath -Raw | ConvertFrom-Json

function Assert-True([bool]$condition, [string]$message) {
    if (!$condition) { throw $message }
}

Assert-True ($component -match 'localeKey,\s*"ReviewText"') 'Review text must use the localized field.'
Assert-True ($component -match 'original \?\? string\.Empty\)\.Trim\(\)\.Length >= 8') 'Long-source corruption guard is missing.'
Assert-True ($component -match 'localized\.Trim\(\)\.Length <= 1') 'Single-character corruption guard is missing.'
Assert-True ($component -notmatch 'localeKey,\s*"Title"[\s\S]{0,250}localized\.Trim\(\)\.Length <= 1') 'Title must not be subject to the single-character guard.'

foreach ($literal in @(
    'Submitting...',
    'An error occurred while submitting the review. Please try again.',
    'Failed to vote. Please refresh the page and try one more time.'
)) {
    Assert-True ($component -notmatch [regex]::Escape($literal)) "Hard-coded English remains in component: $literal"
    foreach ($path in $helpfulnessPaths) {
        $content = Get-Content -LiteralPath $path -Raw
        Assert-True ($content -notmatch [regex]::Escape($literal)) "Hard-coded English remains in $path : $literal"
    }
}

foreach ($resourceKey in @(
    'Plugins.Widgets.CustomProductReviews.Review.Submitting',
    'Plugins.Widgets.CustomProductReviews.Review.SubmitError'
)) {
    Assert-True ($component.Contains($resourceKey)) "Component does not use $resourceKey"
}

foreach ($path in $helpfulnessPaths) {
    $content = Get-Content -LiteralPath $path -Raw
    Assert-True ($content.Contains('Plugins.Widgets.CustomProductReviews.Review.VoteError')) "Helpfulness partial does not use localized vote error: $path"
    Assert-True ($content.Contains('JsonSerializer.Serialize')) "Helpfulness message is not emitted as a safe JavaScript string: $path"
}

Assert-True ($component.Contains('JsonSerializer.Serialize')) 'Component messages must be emitted as safe JavaScript strings.'
Assert-True ($pluginClass.Contains('ReviewLocalizationResources.GetPluginResources(languageCode)')) 'Plugin update/install path must seed only the plugin-owned resource catalog.'
Assert-True (-not $pluginClass.Contains('ReviewLocalizationResources.GetResources(languageCode)')) 'Plugin updates must not repeatedly overwrite corrected core resources.'
Assert-True ([string]$pluginJson.Version -eq '1.15') 'Plugin version must be bumped so UpdateAsync can seed corrected resources.'

Write-Host 'Review view localization static contracts: PASS' -ForegroundColor Green
