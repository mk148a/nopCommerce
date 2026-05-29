HOOD CustomProductReviews Source Fix 1.08.2

This is the full plugin source tree, not a runtime hotfix.

Use:
1. Backup your current plugin source folder.
2. Copy/overwrite these files into your existing Nop.Plugin.Widgets.CustomProductReviews source folder.
3. Rebuild the plugin with your nopCommerce 4.80 solution.
4. Deploy the compiled plugin output to:
   /Plugins/Widgets.CustomProductReviews/
5. Restart IIS App Pool.
6. Clear nopCommerce cache and CDN/Cloudflare cache.

Important:
- If the plugin is already installed, InstallAsync does not run again automatically.
- Therefore the source contains Resources/upsert-customproductreviews-resources-v108.sql.
- Run that SQL once against your live nopCommerce database, or uninstall/install the plugin if you can do that safely.
- Safer option: run the SQL file once. It only upserts LocaleStringResource values.

Main changes:
- Adds missing resource keys in canonical and lowercase forms:
  plugins.widgets.customproductreviews.productreviewsfor
  plugins.widgets.customproductreviews.attachfiles
  plugins.widgets.customproductreviews.maxfilesinupload
  plugins.widgets.customproductreviews.hovertozoom
  plugins.widgets.customproductreviews.viewlargerreviewphoto
  plugins.widgets.customproductreviews.reviewvideo
  plugins.widgets.customproductreviews.videonotsupported
- Adds translations for EN/TR/DE/FR/ES/IT/NL/RU/AR/PT/PL/ZH/JA/KO with English fallback.
- Adds Razor fallback so missing resources do not print raw resource keys.
- Fixes review media markup for SEO:
  a href full image + img alt/title/width/height/loading/decoding
- Fixes review item layout.
- Restores hover/tap image zoom inside the plugin.


1.08.2 additional fix:
- Kills old theme-level hood-review-zoom-v107 / hood-review-zoom-overlay ghost nodes.
- Adds CSS protection even if the old theme JS is still cached.
- Adds JS MutationObserver to remove old legacy overlay nodes from DOM.
- Includes optional server cleanup script:
  _server_cleanup_optional/Remove-HoodLegacyReviewZoom.ps1

Recommended one-time cleanup after deploying source:
cd <extracted package>\_server_cleanup_optional
Set-ExecutionPolicy -Scope Process Bypass
.\Remove-HoodLegacyReviewZoom.ps1 -SiteRoot "C:\inetpub\wwwroot\hoodarcheryshop.com"

Then clear nopCommerce cache and purge CDN/Cloudflare.
