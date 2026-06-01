USE [HoodArcheryShopV480bugfixLancelotDb];
SET NOCOUNT ON;

/*
    HOOD CustomProductReviews v1.11.3 resource repair.
    This script upserts missing plugin locale resources for every existing language.
    It is safe to run more than once.
*/

DECLARE @Resources TABLE
(
    ResourceName nvarchar(200) NOT NULL PRIMARY KEY,
    ResourceValue nvarchar(max) NOT NULL
);

INSERT INTO @Resources (ResourceName, ResourceValue)
VALUES
(N'plugins.widgets.customproductreviews.configuration', N'Custom product reviews settings'),
(N'plugins.widgets.customproductreviews.configuration.adminmedia', N'Admin product review list media'),
(N'plugins.widgets.customproductreviews.configuration.uploadhint', N'Default/current large upload setting: 1073741824 bytes ≈ 1 GB.'),
(N'plugins.widgets.customproductreviews.fields.enabled', N'Enable'),
(N'plugins.widgets.customproductreviews.fields.enabled.hint', N'Check to activate this widget.'),
(N'plugins.widgets.customproductreviews.fields.script', N'Installation script'),
(N'plugins.widgets.customproductreviews.fields.script.hint', N'Find your unique installation script on the Installation tab in your account and then copy it into this field.'),
(N'plugins.widgets.customproductreviews.fields.script.required', N'Installation script is required'),
(N'plugins.widgets.customproductreviews.fields.widgetzone', N'Widget zone'),
(N'plugins.widgets.customproductreviews.fields.widgetzone.hint', N'Widget zone where the custom product review widget is rendered.'),
(N'plugins.widgets.customproductreviews.fields.maximumfile', N'Maximum files per review'),
(N'plugins.widgets.customproductreviews.fields.maximumfile.hint', N'Maximum number of uploaded media files accepted for one product review.'),
(N'plugins.widgets.customproductreviews.fields.maximumsize', N'Maximum upload size (bytes)'),
(N'plugins.widgets.customproductreviews.fields.maximumsize.hint', N'Maximum file size allowed for each uploaded review photo or video, in bytes.'),
(N'plugins.widgets.customproductreviews.fields.adminshowmediaonproductreviewlist', N'Show media in admin product review list'),
(N'plugins.widgets.customproductreviews.fields.adminshowmediaonproductreviewlist.hint', N'Show product review photo/video thumbnails on Admin > Catalog > Product reviews.'),
(N'plugins.widgets.customproductreviews.fields.adminmediathumbsize', N'Admin media thumbnail size (px)'),
(N'plugins.widgets.customproductreviews.fields.adminmediathumbsize.hint', N'Thumbnail width/height in pixels for admin product review media previews.'),
(N'plugins.widgets.customproductreviews.fields.adminmediamaxitemsperreview', N'Max media items per review row'),
(N'plugins.widgets.customproductreviews.fields.adminmediamaxitemsperreview.hint', N'Maximum number of photos/videos shown in one admin product review row.'),
(N'plugins.widgets.customproductreviews.fields.publiccompactreviewlayout', N'Use compact public review layout'),
(N'plugins.widgets.customproductreviews.fields.publiccompactreviewlayout.hint', N'Reduce empty space in public product review layout.'),
(N'plugins.widgets.customproductreviews.productreviewsfor', N'Product reviews for'),
(N'Product Reviews For', N'Product reviews for'),
(N'Product Reviews For ', N'Product reviews for'),
(N'plugins.widgets.customproductreviews.attachfiles', N'Attach files'),
(N'plugins.widgets.customproductreviews.maxfilesinupload', N'Maximum files in upload: {0}'),
(N'plugins.widgets.customproductreviews.mediaprocessingstarted', N'Your uploaded media (photo or video) will continue to be processed in the background.'),
(N'plugins.widgets.customproductreviews.mediaprocessingcompletedautomatically', N'After processing, the media will be automatically added to your review.'),
(N'plugins.widgets.customproductreviews.unsupportedfileformat', N'File format is not supported for upload.'),
(N'plugins.widgets.customproductreviews.uploadfileformaterror', N'Upload file format error.'),
(N'plugins.widgets.customproductreviews.generalerror', N'A general error occurred. Please try again.'),
(N'plugins.widgets.customproductreviews.viewlargerreviewphoto', N'View larger review photo'),
(N'plugins.widgets.customproductreviews.hovertozoom', N'Hover to enlarge'),
(N'plugins.widgets.customproductreviews.videonotsupported', N'Your browser does not support the video tag.'),
(N'plugins.widgets.customproductreviews.reviewvideo', N'Review video'),
(N'plugins.widgets.customproductreviews.adminmedia', N'Review media'),
(N'plugins.widgets.customproductreviews.noadminmedia', N'No media');

MERGE dbo.LocaleStringResource AS target
USING (
    SELECT l.Id AS LanguageId, r.ResourceName, r.ResourceValue
    FROM dbo.[Language] l
    CROSS JOIN @Resources r
) AS src
ON target.LanguageId = src.LanguageId
AND LOWER(target.ResourceName) = LOWER(src.ResourceName)
WHEN MATCHED THEN
    UPDATE SET ResourceValue = src.ResourceValue
WHEN NOT MATCHED THEN
    INSERT (LanguageId, ResourceName, ResourceValue)
    VALUES (src.LanguageId, src.ResourceName, src.ResourceValue);

SELECT
    l.Id AS LanguageId,
    l.Name AS LanguageName,
    COUNT(*) AS CustomProductReviewResourceCount
FROM dbo.[Language] l
JOIN dbo.LocaleStringResource r ON r.LanguageId = l.Id
WHERE r.ResourceName LIKE N'plugins.widgets.customproductreviews%'
   OR r.ResourceName LIKE N'Plugins.Widgets.CustomProductReviews%'
   OR r.ResourceName IN (N'Product Reviews For', N'Product Reviews For ', N'product reviews for')
GROUP BY l.Id, l.Name
ORDER BY l.Id;
