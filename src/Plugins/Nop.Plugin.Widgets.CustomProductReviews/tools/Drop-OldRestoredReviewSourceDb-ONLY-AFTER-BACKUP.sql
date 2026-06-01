USE master;
SET NOCOUNT ON;

DECLARE @OldDb sysname = N'HoodArcheryShopV480_OLD_yedekeski';
DECLARE @DoDrop bit = 0; -- 0 = report only, 1 = drop old restored DB

SELECT
    DB_NAME(database_id) AS DatabaseName,
    SUM(size) * 8.0 / 1024 AS SizeMB
FROM sys.master_files
WHERE DB_NAME(database_id) = @OldDb
GROUP BY database_id;

IF @DoDrop = 1
BEGIN
    IF DB_ID(@OldDb) IS NULL
    BEGIN
        PRINT 'Old restored DB not found.';
        RETURN;
    END;

    DECLARE @sql nvarchar(max) = N'ALTER DATABASE ' + QUOTENAME(@OldDb) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ' + QUOTENAME(@OldDb) + N';';
    PRINT @sql;
    EXEC sys.sp_executesql @sql;
END
ELSE
BEGIN
    PRINT 'Report only. Set @DoDrop = 1 only after final live DB backup is confirmed.';
END;
