HOOD CustomProductReviews 1.11.3

Fixes:
- Adds missing admin configuration localization resources.
- Adds lower-case resource entries because nopCommerce lookup often normalizes resource keys.
- Adds a shared CustomProductReviewsLocaleResources helper.
- Configure page self-repairs missing resources on GET/POST.
- Adds SQL helper: Resources/upsert-customproductreviews-admin-resources-v113.sql

If the Configure page still shows raw keys:
1) Build and deploy this plugin package.
2) Restart the app pool.
3) Open /Admin/CustomProductReviewsAdmin/Configure once.
4) Clear nopCommerce cache.

Optional immediate DB repair:
Run Resources/upsert-customproductreviews-admin-resources-v113.sql, then clear cache/restart.
