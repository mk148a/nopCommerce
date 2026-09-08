param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$pluginRoot = Split-Path -Parent $PSScriptRoot
$logos = @(
    @{ Directory = 'Nop.Plugin.Payments.StripeKlarna'; Text = 'Klarna.'; Background = '#FFB3C7'; Foreground = '#111111'; FontSize = 23 },
    @{ Directory = 'Nop.Plugin.Payments.StripeAffirm'; Text = 'affirm'; Background = '#FFFFFF'; Foreground = '#4A4AF4'; FontSize = 22 },
    @{ Directory = 'Nop.Plugin.Payments.StripeAfterpay'; Text = 'Afterpay'; Background = '#B2FCE4'; Foreground = '#111111'; FontSize = 19 },
    @{ Directory = 'Nop.Plugin.Payments.StripeZip'; Text = 'zip'; Background = '#5C2D91'; Foreground = '#FFFFFF'; FontSize = 25 }
)

foreach ($logo in $logos) {
    $target = Join-Path $pluginRoot "$($logo.Directory)\logo.png"
    $bitmap = [System.Drawing.Bitmap]::new(128, 40)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
            $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
            $graphics.Clear([System.Drawing.ColorTranslator]::FromHtml($logo.Background))

            $font = [System.Drawing.Font]::new('Arial', $logo.FontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
            $brush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml($logo.Foreground))
            $format = [System.Drawing.StringFormat]::new()
            try {
                $format.Alignment = [System.Drawing.StringAlignment]::Center
                $format.LineAlignment = [System.Drawing.StringAlignment]::Center
                $graphics.DrawString($logo.Text, $font, $brush, [System.Drawing.RectangleF]::new(0, 0, 128, 40), $format)
            }
            finally {
                $format.Dispose()
                $brush.Dispose()
                $font.Dispose()
            }
        }
        finally {
            $graphics.Dispose()
        }

        $bitmap.Save($target, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $bitmap.Dispose()
    }
}
