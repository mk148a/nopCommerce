HOOD CustomProductReviews 1.11

Changes:
- Adds plugin configuration page: /Admin/CustomProductReviewsAdmin/Configure
- GetConfigurationPageUrl now opens the plugin Configure page from admin plugin list.
- Adds visible settings:
  - WidgetZone
  - MaximumFile
  - MaximumSize
  - AdminShowMediaOnProductReviewList
  - AdminMediaThumbSize
  - AdminMediaMaxItemsPerReview
  - PublicCompactReviewLayout
- Adds admin ProductReview/List media preview injection without replacing nopCommerce admin views.
- Adds JSON endpoint: /Admin/CustomProductReviewsAdmin/ReviewMediaSummary?productReviewIds=...
- Adds cleanup helper SQL:
  - tools/Diagnose-And-Cleanup-LegacyProductReviewTables.sql
  - tools/Drop-OldRestoredReviewSourceDb-ONLY-AFTER-BACKUP.sql

Deployment:
1) Build plugin against nopCommerce 4.80 source.
2) Copy output to Presentation/Nop.Web/Plugins/Widgets.CustomProductReviews.
3) Restart app pool / site.
4) Go to Admin > Configuration > Local plugins > Custom Product Reviews > Configure.
5) Enable AdminShowMediaOnProductReviewList.
6) Open Admin/ProductReview/List and check thumbnails/videos under Review text.

Safety:
- Do not drop ProductReviewsTransactionsMapping unless you confirm Etsy source display is no longer needed.
- Do not drop CustomProductReviewMapping, Picture, PictureBinary, ProductReviewVideo, ProductReviewVideoBinary.
