SELECT
    l.Id AS LanguageId,
    l.Name,
    l.LanguageCulture,
    l.UniqueSeoCode,
    COUNT(CASE WHEN lp.LocaleKey = N'Title' THEN 1 END) AS TitleTranslationCount,
    COUNT(CASE WHEN lp.LocaleKey = N'ReviewText' THEN 1 END) AS ReviewTextTranslationCount
FROM [Language] l
LEFT JOIN LocalizedProperty lp
    ON lp.LanguageId = l.Id
   AND lp.LocaleKeyGroup = N'ProductReview'
   AND lp.LocaleKey IN (N'Title', N'ReviewText')
WHERE l.Published = 1
GROUP BY l.Id, l.Name, l.LanguageCulture, l.UniqueSeoCode
ORDER BY l.Id;

SELECT TOP 50
    pr.Id AS ReviewId,
    pr.ProductId,
    LEFT(ISNULL(pr.Title,''), 120) AS OriginalTitle,
    LEFT(ISNULL(pr.ReviewText,''), 200) AS OriginalReviewText
FROM ProductReview pr
WHERE pr.IsApproved = 1
ORDER BY pr.Id DESC;