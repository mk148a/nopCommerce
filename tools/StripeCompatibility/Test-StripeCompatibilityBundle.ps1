[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BundleRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (Test-Path -LiteralPath $BundleRoot -PathType Leaf) {
    $extractRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("stripe-compat-test-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $extractRoot -Force | Out-Null
    Expand-Archive -LiteralPath $BundleRoot -DestinationPath $extractRoot -Force
    $root = $extractRoot
}
else {
    $root = (Resolve-Path $BundleRoot).Path
    $extractRoot = $null
}

try {
    $manifestPath = Join-Path $root 'compatibility-manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath)) { throw 'compatibility-manifest.json is missing' }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.stripeNet -ne '52.2.0') { throw "Unexpected Stripe.net version: $($manifest.stripeNet)" }
    if ($manifest.licenseControl -ne 'disabled-by-source-absence') { throw 'Bundle is not the unlicensed build' }

    foreach ($version in @('4.50', '4.60', '4.70', '4.80')) {
        foreach ($plugin in @(
                @{ Name = 'Nop.Plugin.Payments.Stripe'; Dll = 'Nop.Plugin.Payments.Stripe.dll'; Logo = 'logo.png'; Asset = 'Content\css\StripeAdminStyle.min.css' },
                @{ Name = 'Nop.Plugin.Payments.StripeApplePay'; Dll = 'Nop.Plugin.Payments.StripeApplePay.dll'; Logo = 'logo.jpg'; Asset = 'Contents\stripe.js' })) {
            $path = Join-Path $root "$version\Plugins\$($plugin.Name)"
            if (-not (Test-Path -LiteralPath $path)) { throw "Missing plugin folder: $path" }
            foreach ($required in @($plugin.Dll, 'plugin.json', 'Stripe.net.dll', $plugin.Logo, $plugin.Asset)) {
                if (-not (Test-Path -LiteralPath (Join-Path $path $required))) { throw "Missing $required in $path" }
            }
            $pluginManifest = Get-Content -LiteralPath (Join-Path $path 'plugin.json') -Raw | ConvertFrom-Json
            if (@($pluginManifest.SupportedVersions) -notcontains $version) { throw "Manifest version mismatch in $path" }
            $forbidden = Get-ChildItem -LiteralPath $path -Recurse -File | Where-Object Name -match '^(Nop\.(Core|Data|Services|Web)|Microsoft\.|System\.)|(?i)(license|activation|genius)'
            if ($forbidden) { throw "Host or license file leaked into ${path}: $($forbidden.Name -join ', ')" }
            $stripeVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $path 'Stripe.net.dll')).FileVersion
            if ($stripeVersion -ne '52.2.0.0') { throw "Unexpected Stripe.net file version $stripeVersion in $path" }
        }
    }
    Write-Host 'Stripe compatibility bundle validation passed: 4 versions, 8 plugin folders, no host/license files.'
}
finally {
    if ($extractRoot -and (Test-Path -LiteralPath $extractRoot)) { Remove-Item -LiteralPath $extractRoot -Recurse -Force -ErrorAction SilentlyContinue }
}
