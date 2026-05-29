Nop.Plugin.Widgets.CustomProductReviews - HOOD FIX 1.06

Scope:
- SEO-friendly review media HTML: review images are real <a href="full-image"> links with alt/title/aria-label.
- Hover zoom restored inside the plugin via Content/js/review-media.js.
- Removed duplicate hidden full-size image block that caused overlap.
- Review media layout fixed: thumbnail stays small, helpfulness row stays below media.
- Review videos use preload="metadata", playsinline, width/height, aria-label.
- Product review page strings moved to plugin locale resources.
- Resource alias keys included to stop old warning logs for "Product reviews for".
- Runtime PowerShell script included to copy Razor/CSS/JS files into the live plugin folder and insert/update LocaleStringResource rows for your active language IDs.

Immediate runtime apply without rebuilding DLL:
1) Extract this zip on the server.
2) PowerShell as Administrator:

   cd C:\Temp\Nop.Plugin.Widgets.CustomProductReviews.HoodFix-1.06\tools
   Set-ExecutionPolicy -Scope Process Bypass
   .\Apply-HoodCustomProductReviewsRuntimeFix.ps1 -SiteRoot "C:\inetpub\wwwroot\hoodarcheryshop.com"

3) Clear nopCommerce cache.
4) Purge CDN/Cloudflare cache.
5) Open product review page in incognito.
6) Console:

   CPR_reviewMediaReport && CPR_reviewMediaReport()

Expected:
- links > 0 on review pages with images.
- review photo is thumbnail-size.
- hover shows large preview on desktop.
- mobile tap opens preview.
- helpfulness row does not overlap.

Rebuild note:
- Runtime script copies views/css/js and resources; it does not replace the compiled DLL.
- C# controller improvements require rebuilding the plugin DLL from this patched source.
