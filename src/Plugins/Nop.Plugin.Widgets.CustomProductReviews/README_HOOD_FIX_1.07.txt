HOOD CustomProductReviews Fix 1.07

Fixes:
1. Product review title no longer prints resource key when DB resource is missing.
2. Adds SQL upsert for canonical and lowercase resource names for every active Language row.
3. Desktop review layout is aligned as two columns: avatar/date/rating on left, text/media on right.
4. Removes large empty vertical gaps in review items.
5. Review image output is SEO-friendly anchor + image.
6. Replaces old hover popup with JS overlay that works with new markup and legacy .thumb-item markup.
7. Cleans old theme-level review hacks v10.4-v10.7 from _Root.Head.cshtml by default.

Install:
PowerShell as Administrator:

cd C:\inetpub\wwwroot\Nop.Plugin.Widgets.CustomProductReviews.HoodFix-1.07-2026-05-29\tools
Set-ExecutionPolicy -Scope Process Bypass
.\Apply-HoodCustomProductReviewsRuntimeFix.ps1 -SiteRoot "C:\inetpub\wwwroot\hoodarcheryshop.com"

After:
1. Clear nopCommerce cache.
2. Purge CDN / Cloudflare cache.
3. Open the review page in incognito.

Console:
console.log(CPR_reviewMediaReport && CPR_reviewMediaReport());

Expected:
- version: 1.07.0-hood
- links > 0 on new patched markup
- legacyImages may also be > 0 if old view cache remains
- hover over review image opens large preview
- resource key is not printed in title
