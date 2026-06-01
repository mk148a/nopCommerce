HOOD SOURCE FIX 1.10

Changes:
1. Review title block is rendered only when Title is not blank.
2. Added compact review layout CSS to remove large empty gaps between avatar/source column and review text/media.
3. Added tools/Migrate-CustomProductReviewMediaMappings.sql to restore CustomProductReviewMapping media rows from the old restored DB.

Install:
- Copy Content/review-media.css, Content/style.css, Content/style.min.css to the plugin folder.
- Copy Views/Product/_ProductReviews.cshtml, Views/ProductReviewComponent.cshtml and Themes/Element/Views/Product/_ProductReviews.cshtml if those files are used on the server.
- Run tools/Migrate-CustomProductReviewMediaMappings.sql first with @Commit = 0, then @Commit = 1 only if dry-run is correct.
- Clear nopCommerce cache and restart the app pool/site.
