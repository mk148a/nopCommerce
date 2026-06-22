USE [HoodArcheryShopV480bugfixLancelotDb];
SET NOCOUNT ON;

/*
    HOOD CustomProductReviews cleanup helper.
    SAFE DEFAULT: @Action = 'REPORT' writes nothing.

    Recommended:
    1) Run REPORT.
    2) Do not delete these live tables: CustomProductReviewMapping, ProductReviewVideo, ProductReviewVideoBinary,
       Picture, PictureBinary, ProductReview, ProductReviewsTransactionsMapping.
    3) Empty legacy tables can be renamed first, not dropped.
*/

DECLARE @Action nvarchar(20) = N'REPORT';
-- REPORT        = only show tables and counts
-- RENAME_EMPTY  = rename empty candidate legacy tables to zzz_archive_...
-- DROP_EMPTY    = drop empty candidate legacy tables only

DECLARE @ArchiveSuffix nvarchar(40) = FORMAT(SYSUTCDATETIME(), 'yyyyMMddHHmmss');

DROP TABLE IF EXISTS #ReviewRelatedTables;
CREATE TABLE #ReviewRelatedTables
(
    SchemaName sysname NOT NULL,
    TableName sysname NOT NULL,
    RowCount bigint NOT NULL DEFAULT 0,
    HasForeignKeysOut bit NOT NULL DEFAULT 0,
    HasForeignKeysIn bit NOT NULL DEFAULT 0,
    KeepReason nvarchar(400) NULL,
    SuggestedAction nvarchar(100) NULL
);

INSERT INTO #ReviewRelatedTables (SchemaName, TableName)
SELECT s.name, t.name
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.is_ms_shipped = 0
  AND (
        t.name LIKE N'%Review%'
        OR t.name LIKE N'%Picture%'
        OR t.name LIKE N'%Video%'
        OR t.name LIKE N'%Etsy%'
        OR t.name LIKE N'%Transaction%'
      );

DECLARE @sql nvarchar(max) = N'';
SELECT @sql += N'
UPDATE #ReviewRelatedTables
SET RowCount = (SELECT COUNT_BIG(*) FROM ' + QUOTENAME(SchemaName) + N'.' + QUOTENAME(TableName) + N')
WHERE SchemaName = N''' + REPLACE(SchemaName, '''', '''''') + N''' AND TableName = N''' + REPLACE(TableName, '''', '''''') + N''';'
FROM #ReviewRelatedTables;
EXEC sys.sp_executesql @sql;

UPDATE r
SET HasForeignKeysOut = 1
FROM #ReviewRelatedTables r
WHERE EXISTS (
    SELECT 1
    FROM sys.foreign_keys fk
    JOIN sys.tables t ON t.object_id = fk.parent_object_id
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = r.SchemaName AND t.name = r.TableName
);

UPDATE r
SET HasForeignKeysIn = 1
FROM #ReviewRelatedTables r
WHERE EXISTS (
    SELECT 1
    FROM sys.foreign_keys fk
    JOIN sys.tables t ON t.object_id = fk.referenced_object_id
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = r.SchemaName AND t.name = r.TableName
);

UPDATE #ReviewRelatedTables
SET KeepReason = CASE
    WHEN TableName IN (N'ProductReview', N'CustomProductReviewMapping', N'ProductReviewVideo', N'ProductReviewVideoBinary', N'Picture', N'PictureBinary', N'ProductReviewsTransactionsMapping')
        THEN N'KEEP: current review/media/source flow uses or may use this table.'
    WHEN RowCount > 0
        THEN N'KEEP/VERIFY: table is not empty. Do not drop without manual verification.'
    ELSE NULL
END;

UPDATE #ReviewRelatedTables
SET SuggestedAction = CASE
    WHEN KeepReason IS NOT NULL THEN N'KEEP'
    WHEN RowCount = 0 THEN N'CAN_RENAME_OR_DROP_EMPTY_AFTER_BACKUP'
    ELSE N'REVIEW_MANUALLY'
END;

SELECT *
FROM #ReviewRelatedTables
ORDER BY
    CASE WHEN SuggestedAction = N'CAN_RENAME_OR_DROP_EMPTY_AFTER_BACKUP' THEN 0 ELSE 1 END,
    TableName;

IF @Action IN (N'RENAME_EMPTY', N'DROP_EMPTY')
BEGIN
    DECLARE @SchemaName sysname, @TableName sysname, @Command nvarchar(max);
    DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
        SELECT SchemaName, TableName
        FROM #ReviewRelatedTables
        WHERE SuggestedAction = N'CAN_RENAME_OR_DROP_EMPTY_AFTER_BACKUP'
          AND RowCount = 0
          AND HasForeignKeysIn = 0
          AND HasForeignKeysOut = 0;

    OPEN cur;
    FETCH NEXT FROM cur INTO @SchemaName, @TableName;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF @Action = N'RENAME_EMPTY'
        BEGIN
            SET @Command = N'EXEC sp_rename N''' + QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@TableName) + N''', N''zzz_archive_' + @TableName + N'_' + @ArchiveSuffix + N''';';
        END
        ELSE
        BEGIN
            SET @Command = N'DROP TABLE ' + QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@TableName) + N';';
        END;

        PRINT @Command;
        EXEC sys.sp_executesql @Command;

        FETCH NEXT FROM cur INTO @SchemaName, @TableName;
    END
    CLOSE cur;
    DEALLOCATE cur;
END;
