[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BaseUrl,

    [string]$HostHeader,

    [switch]$SkipCertificateCheck
)

$ErrorActionPreference = 'Stop'

function Get-Page([string]$Path) {
    $options = @{
        Uri = "$($BaseUrl.TrimEnd('/'))$Path"
        UseBasicParsing = $true
        TimeoutSec = 60
        Headers = @{ 'Cache-Control' = 'no-cache' }
    }

    if ($HostHeader) {
        $options.Headers.Host = $HostHeader
    }

    if ($SkipCertificateCheck) {
        $options.SkipCertificateCheck = $true
    }

    Invoke-WebRequest @options
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw $Message
    }
}

$homeResponse = Get-Page '/en/?ui-layout-smoke=1'
Assert-True ($homeResponse.StatusCode -eq 200) 'Homepage did not return 200.'
Assert-True ($homeResponse.Content -match 'archery-category-item') 'Homepage is missing the semantic Archery category class.'
Assert-True ($homeResponse.Content -notmatch 'width="635" height="635"') 'Homepage still contains the legacy fixed 635px image dimensions.'
Assert-True ($homeResponse.Content -notmatch 'lib_npm/fine-uploader') 'Fine Uploader remains registered on the homepage.'

$styleUrl = [regex]::Match($homeResponse.Content, 'href="(?<url>[^"]*styles\.css(?:\?v=[^"]*)?)"').Groups['url'].Value
Assert-True (-not [string]::IsNullOrWhiteSpace($styleUrl)) 'Homepage styles.css URL was not rendered.'

$styles = Get-Page $styleUrl
Assert-True ($styles.Content -match 'home-page-product-grid \.product-item \.picture') 'Featured Products media sizing rule is missing.'
Assert-True ($styles.Content -match 'object-fit:\s*contain') 'Non-cropping image fit rule is missing.'

foreach ($asset in @(
    '/lib_npm/fine-uploader/fine-uploader/fine-uploader.min.css',
    '/lib_npm/fine-uploader/jquery.fine-uploader/jquery.fine-uploader.min.js'
)) {
    $response = Get-Page $asset
    Assert-True ($response.StatusCode -eq 200) "$asset did not return 200."
    Assert-True (-not [string]::IsNullOrWhiteSpace($response.Headers['Content-Type'])) "$asset has no Content-Type."
}

foreach ($path in @('/en/archery-2', '/en/medieval-clothing')) {
    Assert-True ((Get-Page $path).StatusCode -eq 200) "$path did not return 200."
}

Write-Output 'Homepage image-layout smoke test passed.'
