SELECT
    l.Id AS LanguageId,
    l.Name,
    l.UniqueSeoCode,
    COUNT(CASE WHEN lp.LocaleKey = N'Title' THEN 1 END) AS ReviewTitleTranslations,
    COUNT(CASE WHEN lp.LocaleKey = N'ReviewText' THEN 1 END) AS ReviewTextTranslations
FROM [Language] l
LEFT JOIN LocalizedProperty lp
    ON lp.LanguageId = l.Id
   AND lp.LocaleKeyGroup = N'ProductReview'
   AND lp.LocaleKey IN (N'Title', N'ReviewText')
WHERE l.Published = 1
GROUP BY l.Id, l.Name, l.UniqueSeoCode
ORDER BY l.Id;