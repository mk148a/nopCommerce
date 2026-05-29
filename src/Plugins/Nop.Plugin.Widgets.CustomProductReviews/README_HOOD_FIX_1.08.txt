HOOD CustomProductReviews Fix 1.08

Fixes visible issue:
- plugins.widgets.customproductreviews.attachfiles
- plugins.widgets.customproductreviews.productreviewsfor
- plugins.widgets.customproductreviews.hovertozoom
- plugins.widgets.customproductreviews.viewlargerreviewphoto
- plugins.widgets.customproductreviews.reviewvideo
- plugins.widgets.customproductreviews.videonotsupported
- plugins.widgets.customproductreviews.maxfilesinupload

What changed:
1. Inserts BOTH canonical and lowercase resource keys.
2. Applies resources to every active language row in nopCommerce [Language].
3. Adds translations for EN/TR/DE/FR/ES/IT/NL/RU/AR/PT/PL/ZH/JA/KO.
4. Unknown languages fall back to English instead of showing resource keys.
5. Adds Razor fallback in review views, so missing resource keys never print on page.
6. Keeps review image SEO markup + hover/tap zoom from 1.07.

Install:
PowerShell as Administrator:

cd C:\inetpub\wwwroot\Nop.Plugin.Widgets.CustomProductReviews.HoodFix-1.08-2026-05-29\tools
Set-ExecutionPolicy -Scope Process Bypass
.\Apply-HoodCustomProductReviewsRuntimeFix.ps1 -SiteRoot "C:\inetpub\wwwroot\hoodarcheryshop.com"

If only resource strings are still missing:
.\Apply-HoodCustomProductReviewsResourcesOnly.ps1 -SiteRoot "C:\inetpub\wwwroot\hoodarcheryshop.com"

After install:
1. Clear nopCommerce cache.
2. Purge CDN/Cloudflare cache.
3. Open product page in incognito.

SQL verification:
SELECT LanguageId, ResourceName, ResourceValue
FROM LocaleStringResource
WHERE LOWER(ResourceName) LIKE 'plugins.widgets.customproductreviews.%'
ORDER BY LanguageId, ResourceName;
