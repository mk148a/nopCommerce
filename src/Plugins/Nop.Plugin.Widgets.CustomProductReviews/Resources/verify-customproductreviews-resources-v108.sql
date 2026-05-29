SELECT LanguageId, ResourceName, ResourceValue
FROM LocaleStringResource
WHERE LOWER(ResourceName) IN (
  'plugins.widgets.customproductreviews.productreviewsfor',
  'plugins.widgets.customproductreviews.attachfiles',
  'plugins.widgets.customproductreviews.maxfilesinupload',
  'plugins.widgets.customproductreviews.hovertozoom',
  'plugins.widgets.customproductreviews.viewlargerreviewphoto',
  'plugins.widgets.customproductreviews.reviewvideo',
  'plugins.widgets.customproductreviews.videonotsupported'
)
ORDER BY LanguageId, ResourceName;