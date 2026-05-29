HOOD CustomProductReviews Source Fix 1.09

This package builds on 1.08.2 and adds two important fixes:

1) Product review localization display
- Existing review Title / ReviewText / ReplyText are read from nopCommerce LocalizedProperty table when available.
- LocaleKeyGroup: ProductReview
- LocaleKey: Title, ReviewText, ReplyText
- If no translation exists, original review text is displayed.

2) Old /{lang}/productreviews/{productId} 404 handling
- Middleware in Infrastructure/PluginNopStartup.cs catches old productreviews URLs.
- Existing products: 301 redirect to /{lang}/{product-se-name}#product-reviews
- Missing/deleted products: 410 Gone
- This prevents repeated nopCommerce 404 logs for stale localized review URLs.

3) Translation tool
- Tools/Translate-ProductReviews-DeepL.ps1 translates approved reviews into all published languages and upserts LocalizedProperty.
- DeepL is used because it supports direct API text translation with multiple text items per request.
- Google Cloud Translation can also be used later, but it requires a Google Cloud project and service account setup.

Build/deploy:
1. Backup your current plugin source.
2. Copy this package over Nop.Plugin.Widgets.CustomProductReviews source.
3. Rebuild plugin.
4. Deploy compiled output to /Plugins/Widgets.CustomProductReviews/.
5. Recycle IIS App Pool.
6. Clear nopCommerce + CDN cache.

Translate reviews:
PowerShell as Administrator:

cd <this package>\Tools
Set-ExecutionPolicy -Scope Process Bypass
.\Translate-ProductReviews-DeepL.ps1 `
  -SiteRoot "C:\inetpub\wwwroot\hoodarcheryshop.com" `
  -DeepLAuthKey "YOUR_DEEPL_API_KEY" `
  -UseFreeDeepL `
  -Limit 20

Remove -Limit after testing.
Use -DryRun first if you want to see what will be translated without writing DB.

Verify:
Run Tools/Verify-ProductReview-Translations.sql against the live DB.
