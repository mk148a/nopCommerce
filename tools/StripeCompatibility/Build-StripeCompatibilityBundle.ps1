[CmdletBinding()]
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$OutputRoot = (Join-Path $env:TEMP ("nop-stripe-compat-" + (Get-Date -Format 'yyyyMMdd-HHmmss'))),
    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

<#
    Builds the two payment plugins against the nopCommerce versions that are
    actually supported by this repository. A single .NET binary cannot span
    nopCommerce major versions, so the result is one pair of plugin folders per
    version. This is deliberately an unlicensed build: no licensing assembly,
    activation call, or license gate is added to either plugin.
#>

$matrix = @(
    [pscustomobject]@{ Version = '4.50'; Framework = 'net6.0'; CoreRef = 'origin/StripePlugin'; CardRef = 'origin/StripePlugin'; WalletRef = 'origin/StripeApplePayPlugin' },
    [pscustomobject]@{ Version = '4.60'; Framework = 'net7.0'; CoreRef = 'upstream/4.60-bug-fixes'; CardRef = 'origin/StripePlugin'; WalletRef = 'origin/StripeApplePayPlugin' },
    [pscustomobject]@{ Version = '4.70'; Framework = 'net8.0'; CoreRef = 'origin/Lancelot-4.70-bugfix'; CardRef = 'origin/StripePlugin'; WalletRef = 'origin/StripeApplePayPlugin' },
    [pscustomobject]@{ Version = '4.80'; Framework = 'net9.0'; CoreRef = 'HEAD'; CardRef = 'HEAD'; WalletRef = 'HEAD' }
)

$pluginNames = @('Nop.Plugin.Payments.Stripe', 'Nop.Plugin.Payments.StripeApplePay')
$sourcePluginRoot = Join-Path $RepoRoot 'src\Plugins'

function Invoke-Git([string[]]$Arguments) {
    & git -C $RepoRoot @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed with exit code $LASTEXITCODE"
    }
}

function Assert-GitRef([string]$Ref) {
    & git -C $RepoRoot rev-parse --verify "$Ref^{commit}" *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "Required source ref '$Ref' is not available. Fetch the repository refs before building the compatibility bundle."
    }
}

function New-SourceArchive([string]$Ref, [string]$Destination, [string]$Name) {
    Assert-GitRef $Ref
    $zip = Join-Path $Destination "$Name.zip"
    Invoke-Git @('archive', '--format=zip', "--output=$zip", $Ref)
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    Expand-Archive -LiteralPath $zip -DestinationPath (Join-Path $Destination $Name) -Force
    Remove-Item -LiteralPath $zip -Force
    return (Join-Path $Destination $Name)
}

function Write-Utf8([string]$Path, [string]$Text) {
    [System.IO.File]::WriteAllText($Path, $Text, [System.Text.UTF8Encoding]::new($false))
}

function Replace-Required([string]$Path, [string]$Pattern, [string]$Replacement) {
    $text = Get-Content -LiteralPath $Path -Raw
    $updated = [regex]::Replace($text, $Pattern, $Replacement)
    if ($updated -eq $text) {
        throw "Expected source pattern was not found in ${Path}: $Pattern"
    }
    Write-Utf8 $Path $updated
}

function Patch-Plugin([string]$PluginRoot, [string]$Version, [string]$Framework) {
    $csproj = Join-Path $PluginRoot ((Split-Path $PluginRoot -Leaf) + '.csproj')
    if (-not (Test-Path -LiteralPath $csproj)) {
        $csproj = Get-ChildItem -LiteralPath $PluginRoot -Filter '*.csproj' -File | Select-Object -First 1 -ExpandProperty FullName
    }

    Replace-Required $csproj '<TargetFramework>net[0-9.]+</TargetFramework>' "<TargetFramework>$Framework</TargetFramework>"

    $projectText = Get-Content -LiteralPath $csproj -Raw
    if ($projectText -match '<Reference Include="Stripe\.net">[\s\S]*?</Reference>') {
        $projectText = [regex]::Replace($projectText, '<Reference Include="Stripe\.net">[\s\S]*?</Reference>', '    <PackageReference Include="Stripe.net" Version="52.2.0" />')
    }
    elseif ($projectText -notmatch 'PackageReference Include="Stripe\.net"') {
        $projectText = $projectText -replace '</Project>', '  <ItemGroup>\r\n    <PackageReference Include="Stripe.net" Version="52.2.0" />\r\n  </ItemGroup>\r\n</Project>'
    }
    if ($projectText -match '<CopyLocalLockFileAssemblies>') {
        $projectText = [regex]::Replace($projectText, '<CopyLocalLockFileAssemblies>[^<]*</CopyLocalLockFileAssemblies>', '<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>', 1)
    }
    else {
        $projectText = $projectText -replace '</PropertyGroup>', '    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>\r\n  </PropertyGroup>', 1
    }
    if ($projectText -match '<Target Name="NopTarget"[^>]*>') {
        $projectText = [regex]::Replace($projectText, '<Target Name="NopTarget" AfterTargets="Build"(?![^>]*Condition=)', '<Target Name="NopTarget" AfterTargets="Build" Condition="''$(SkipNopClear)'' != ''true''"', 1)
    }
    Write-Utf8 $csproj $projectText

    $manifest = Join-Path $PluginRoot 'plugin.json'
    if (-not (Test-Path -LiteralPath $manifest)) {
        Copy-Item -LiteralPath (Join-Path $sourcePluginRoot (Split-Path $PluginRoot -Leaf) 'plugin.json') -Destination $manifest -Force
    }
    $manifestText = Get-Content -LiteralPath $manifest -Raw
    $manifestReplacement = '"SupportedVersions": [ "' + $Version + '" ]'
    $manifestText = [regex]::Replace($manifestText, '"SupportedVersions"\s*:\s*\[[^\]]*\]', $manifestReplacement, 1)
    Write-Utf8 $manifest $manifestText

    if ($Version -in @('4.60', '4.70')) {
        $codeFiles = Get-ChildItem -LiteralPath $PluginRoot -Filter '*.cs' -File -Recurse
        foreach ($file in $codeFiles) {
            $text = Get-Content -LiteralPath $file.FullName -Raw
            $text = $text.Replace('.IsNullOrEmpty()', '')
            if ($file.Name -eq 'StripePaymentProcessor.cs') {
                $text = $text.Replace('if (customer == null || customer.Id)', 'if (customer == null || string.IsNullOrEmpty(customer.Id))')
                $text = $text.Replace('if (customer.billingAddress.Address1)', 'if (string.IsNullOrEmpty(customer.billingAddress.Address1))')
                $text = $text.Replace('if (!product.Sku)', 'if (!string.IsNullOrEmpty(product.Sku))')
                if ($text -notmatch 'public Type GetPublicViewComponent\(\)') {
                    $needle = '        public string GetPublicViewComponentName()'
                    $insert = "        public Type GetPublicViewComponent()\r\n        {\r\n            return typeof(Nop.Plugin.Payments.Stripe.Components.PaymentStripeViewComponent);\r\n        }\r\n\r\n"
                    $text = $text.Replace($needle, $insert + $needle)
                }
            }
            if ($file.Name -eq 'StripeApplePayPlugin.cs') {
                $text = $text.Replace('using MySqlX.XDevAPI.Common;\r\n', '').Replace('using MySqlX.XDevAPI.Common;\n', '')
                if ($text -notmatch 'public Type GetPublicViewComponent\(\)') {
                    $needle = '        public string GetPublicViewComponentName()'
                    $insert = "        public Type GetPublicViewComponent()\r\n        {\r\n            return typeof(Nop.Plugin.Payments.StripeApplePay.Components.StripeApplePayViewComponent);\r\n        }\r\n\r\n"
                    $text = $text.Replace($needle, $insert + $needle)
                }
            }
            Write-Utf8 $file.FullName $text
        }
        if ($Version -eq '4.70') {
            Get-ChildItem -LiteralPath $PluginRoot -Filter '*.cs' -File -Recurse | ForEach-Object {
                $text = Get-Content -LiteralPath $_.FullName -Raw
                $text = $text.Replace('AreaNames.Admin', 'AreaNames.ADMIN')
                Write-Utf8 $_.FullName $text
            }
        }
    }
}

function Patch-WebCleanup([string]$WebProject) {
    $text = Get-Content -LiteralPath $WebProject -Raw
    $updated = [regex]::Replace($text, '<Target Name="NopTarget" AfterTargets="Build"(?![^>]*Condition=)', '<Target Name="NopTarget" AfterTargets="Build" Condition="''$(SkipPluginCleanup)'' != ''true''"', 1)
    if ($updated -ne $text) { Write-Utf8 $WebProject $updated }
}

function Invoke-Dotnet([string]$File, [string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Arguments -join ' ') failed for $File" }
}

New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
$workRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("stripe-compat-work-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $workRoot -Force | Out-Null
$results = [System.Collections.Generic.List[object]]::new()

try {
    foreach ($entry in $matrix) {
        $versionWork = Join-Path $workRoot $entry.Version
        New-Item -ItemType Directory -Path $versionWork -Force | Out-Null
        $core = New-SourceArchive $entry.CoreRef $versionWork 'core'
        $cardSource = New-SourceArchive $entry.CardRef $versionWork 'card-source'
        $walletSource = New-SourceArchive $entry.WalletRef $versionWork 'wallet-source'
        $pluginsRoot = Join-Path $core 'src\Plugins'
        foreach ($name in $pluginNames) {
            $target = Join-Path $pluginsRoot $name
            if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
        }
        Copy-Item -LiteralPath (Join-Path $cardSource 'src\Plugins\Nop.Plugin.Payments.Stripe') -Destination $pluginsRoot -Recurse -Force
        Copy-Item -LiteralPath (Join-Path $walletSource 'src\Plugins\Nop.Plugin.Payments.StripeApplePay') -Destination $pluginsRoot -Recurse -Force
        foreach ($name in $pluginNames) {
            Patch-Plugin (Join-Path $pluginsRoot $name) $entry.Version $entry.Framework
        }
        $webProject = Join-Path $core 'src\Presentation\Nop.Web\Nop.Web.csproj'
        Patch-WebCleanup $webProject
        $cardProject = Join-Path $pluginsRoot 'Nop.Plugin.Payments.Stripe\Nop.Plugin.Payments.Stripe.csproj'
        $walletProject = Join-Path $pluginsRoot 'Nop.Plugin.Payments.StripeApplePay\Nop.Plugin.Payments.StripeApplePay.csproj'

        if (-not $NoBuild) {
            Invoke-Dotnet $cardProject @('restore', $cardProject, '--nologo', '-v:minimal')
            Invoke-Dotnet $walletProject @('restore', $walletProject, '--nologo', '-v:minimal')
            Invoke-Dotnet $webProject @('build', $webProject, '-c', 'Release', '--nologo', '--no-restore', '-v:minimal', '-p:SkipPluginCleanup=true')
            Invoke-Dotnet $cardProject @('build', $cardProject, '-c', 'Release', '--nologo', '--no-restore', '-v:minimal', '-p:BuildProjectReferences=false', '-p:SkipNopClear=true')
            Invoke-Dotnet $walletProject @('build', $walletProject, '-c', 'Release', '--nologo', '--no-restore', '-v:minimal', '-p:BuildProjectReferences=false', '-p:SkipNopClear=true')
        }

        $publishedRoot = Join-Path $core 'src\Presentation\Nop.Web\Plugins'
        $bundleVersion = Join-Path $OutputRoot $entry.Version
        $bundlePlugins = Join-Path $bundleVersion 'Plugins'
        New-Item -ItemType Directory -Path $bundlePlugins -Force | Out-Null
        foreach ($name in $pluginNames) {
            $built = Join-Path $publishedRoot $name
            $package = Join-Path $bundlePlugins $name
            New-Item -ItemType Directory -Path $package -Force | Out-Null
            foreach ($fileName in @('plugin.json', 'Stripe.net.dll', 'Nop.Plugin.Payments.Stripe.dll', 'Nop.Plugin.Payments.StripeApplePay.dll', 'logo.png', 'logo.jpg')) {
                $file = Join-Path $built $fileName
                if (Test-Path -LiteralPath $file) { Copy-Item -LiteralPath $file -Destination $package -Force }
            }
            foreach ($directoryName in @('Content', 'Contents', 'Views')) {
                $directory = Join-Path $built $directoryName
                if (Test-Path -LiteralPath $directory) { Copy-Item -LiteralPath $directory -Destination $package -Recurse -Force }
            }
            if (-not (Test-Path -LiteralPath (Join-Path $package 'Stripe.net.dll'))) {
                $cardStripe = Join-Path $bundlePlugins 'Nop.Plugin.Payments.Stripe\Stripe.net.dll'
                if (Test-Path -LiteralPath $cardStripe) { Copy-Item -LiteralPath $cardStripe -Destination $package -Force }
            }
        }
        $results.Add([pscustomobject]@{ version = $entry.Version; targetFramework = $entry.Framework; coreRef = $entry.CoreRef; cardRef = $entry.CardRef; walletRef = $entry.WalletRef; status = 'built' })
    }
    $manifest = [pscustomobject]@{
        bundle = 'nopcommerce-stripe-applepay-unlicensed-compatibility'
        generatedUtc = [DateTime]::UtcNow.ToString('o')
        stripeNet = '52.2.0'
        licenseControl = 'disabled-by-source-absence'
        notes = @('One plugin binary is produced per nopCommerce major/minor version.', 'The package contains no license/activation assembly or license network call.', 'Install only the folder matching the target nopCommerce version.')
        versions = $results
    }
    Write-Utf8 (Join-Path $OutputRoot 'compatibility-manifest.json') ($manifest | ConvertTo-Json -Depth 8)
    $zipPath = "$OutputRoot.zip"
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    Compress-Archive -Path (Join-Path $OutputRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal
    Write-Host "Bundle: $zipPath"
}
finally {
    if (Test-Path -LiteralPath $workRoot) { Remove-Item -LiteralPath $workRoot -Recurse -Force }
}
