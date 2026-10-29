param([Parameter(Mandatory = $true)][string]$LiveRoot)
$ErrorActionPreference = 'Stop'
$taskLiveRoot = (Resolve-Path -LiteralPath $LiveRoot).Path
$taskPluginNames = @('Misc.StripeBnplCore', 'Payments.StripeAffirm', 'Payments.StripeKlarna', 'Payments.StripeAfterpay', 'Payments.StripeZip')
foreach ($taskPlugin in $taskPluginNames) {
    & dotnet build (Join-Path $PSScriptRoot 'BuildPlugin.csproj') "-p:PluginName=Nop.Plugin.$taskPlugin" "-p:LiveRoot=$taskLiveRoot" -c Release --nologo -v:q
    if ($LASTEXITCODE -ne 0) { throw "Build failed for $taskPlugin" }
}
$taskManifest = foreach ($taskPlugin in $taskPluginNames) {
    $taskSource = Join-Path $PSScriptRoot "artifacts\Nop.Plugin.$taskPlugin\net9.0\Nop.Plugin.$taskPlugin.dll"
    $taskTarget = "Plugins\$taskPlugin\Nop.Plugin.$taskPlugin.dll"
    [pscustomobject]@{ Source = $taskSource; TargetRelativePath = $taskTarget; NewSha256 = (Get-FileHash -LiteralPath $taskSource).Hash; PreviousSha256 = (Get-FileHash -LiteralPath (Join-Path $taskLiveRoot $taskTarget)).Hash }
    $taskPdbSource = [IO.Path]::ChangeExtension($taskSource, '.pdb')
    $taskPdbTarget = [IO.Path]::ChangeExtension($taskTarget, '.pdb')
    [pscustomobject]@{ Source = $taskPdbSource; TargetRelativePath = $taskPdbTarget; NewSha256 = (Get-FileHash -LiteralPath $taskPdbSource).Hash; PreviousSha256 = (Get-FileHash -LiteralPath (Join-Path $taskLiveRoot $taskPdbTarget)).Hash }
    if ($taskPlugin -ne 'Misc.StripeBnplCore') {
        $taskCoreSource = Join-Path $PSScriptRoot 'artifacts\Nop.Plugin.Misc.StripeBnplCore\net9.0\Nop.Plugin.Misc.StripeBnplCore.dll'
        $taskCoreTarget = "Plugins\$taskPlugin\refs\Nop.Plugin.Misc.StripeBnplCore.dll"
        [pscustomobject]@{ Source = $taskCoreSource; TargetRelativePath = $taskCoreTarget; NewSha256 = (Get-FileHash -LiteralPath $taskCoreSource).Hash; PreviousSha256 = (Get-FileHash -LiteralPath (Join-Path $taskLiveRoot $taskCoreTarget)).Hash }
    }
}
$taskManifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'deployment-manifest.json')
