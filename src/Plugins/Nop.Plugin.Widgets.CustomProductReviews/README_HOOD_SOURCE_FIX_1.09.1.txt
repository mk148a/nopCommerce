HOOD CustomProductReviews Source Fix 1.09.1

This package fixes the DeepL translation script that previously printed:
Languages: 24, reviews: 67
Done. LocalizedProperty rows inserted/updated: 0

Root cause:
The old script could detect reviews/languages but skipped all language targets before API calls.
This script uses robust DataRow column access and prints a Language target map before translating.

Run test:
cd C:\inetpub\wwwroot\hoodarcheryshop.com\Plugins\Widgets.CustomProductReviews\Tools
Set-ExecutionPolicy -Scope Process Bypass
.\Translate-ProductReviews-DeepL.ps1 `
  -SiteRoot "C:\inetpub\wwwroot\hoodarcheryshop.com" `
  -DeepLAuthKey "YOUR_DEEPL_KEY" `
  -UseFreeDeepL `
  -Limit 5

Expected:
- You should see Language target map.
- Non-English rows should show DeepL targets like TR, DE, FR, ES.
- You should see yellow lines like:
  Review #123 -> LanguageId 2 / TR / fields: Title, ReviewText
- LocalizedProperty rows inserted/updated should be greater than 0.

Then run full:
.\Translate-ProductReviews-DeepL.ps1 `
  -SiteRoot "C:\inetpub\wwwroot\hoodarcheryshop.com" `
  -DeepLAuthKey "YOUR_DEEPL_KEY" `
  -UseFreeDeepL
