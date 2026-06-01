USE [HoodArcheryShopV480bugfixLancelotDb];
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Commit bit = 0;
-- 0 = dry-run/rollback, 1 = commit
DECLARE @OldDb sysname = N'HoodArcheryShopV480_OLD_yedekeski';

IF DB_NAME() <> N'HoodArcheryShopV480bugfixLancelotDb'
    THROW 56000, 'Yanlis DB context. Yeni/canli DB uzerinde calismalisin.', 1;
IF DB_ID(@OldDb) IS NULL
    THROW 56001, 'Old DB bulunamadi.', 1;
IF OBJECT_ID(N'dbo.CustomProductReviewMapping', N'U') IS NULL
    THROW 56002, 'Yeni DB dbo.CustomProductReviewMapping bulunamadi. Plugin migration calismamis olabilir.', 1;
IF OBJECT_ID(N'HoodArcheryShopV480_OLD_yedekeski.dbo.CustomProductReviewMapping', N'U') IS NULL
    THROW 56003, 'Eski DB dbo.CustomProductReviewMapping bulunamadi.', 1;

BEGIN TRY
    BEGIN TRAN;

    DROP TABLE IF EXISTS #SourceMappings;
    SELECT
        m.Id AS OldMappingId,
        m.ProductReviewId,
        m.PictureId,
        m.ProductReviewVideoId,
        ISNULL(m.DisplayOrder, 0) AS DisplayOrder
    INTO #SourceMappings
    FROM [HoodArcheryShopV480_OLD_yedekeski].dbo.CustomProductReviewMapping m
    JOIN dbo.ProductReview pr ON pr.Id = m.ProductReviewId
    WHERE (m.PictureId IS NOT NULL OR m.ProductReviewVideoId IS NOT NULL)
      AND NOT EXISTS (
          SELECT 1
          FROM dbo.CustomProductReviewMapping x
          WHERE x.ProductReviewId = m.ProductReviewId
            AND ISNULL(x.PictureId, -1) = ISNULL(m.PictureId, -1)
            AND ISNULL(x.ProductReviewVideoId, -1) = ISNULL(m.ProductReviewVideoId, -1)
      );

    DROP TABLE IF EXISTS #NeededPictures;
    SELECT DISTINCT PictureId
    INTO #NeededPictures
    FROM #SourceMappings
    WHERE PictureId IS NOT NULL;

    DROP TABLE IF EXISTS #NeededVideos;
    SELECT DISTINCT ProductReviewVideoId AS VideoId
    INTO #NeededVideos
    FROM #SourceMappings
    WHERE ProductReviewVideoId IS NOT NULL;

    -- Picture ana kayıtları: Id korunmalı, mapping PictureId buna bağlı.
    IF OBJECT_ID(N'dbo.Picture', N'U') IS NOT NULL
       AND OBJECT_ID(N'HoodArcheryShopV480_OLD_yedekeski.dbo.Picture', N'U') IS NOT NULL
       AND EXISTS (SELECT 1 FROM #NeededPictures np WHERE NOT EXISTS (SELECT 1 FROM dbo.Picture p WHERE p.Id = np.PictureId))
    BEGIN
        SET IDENTITY_INSERT dbo.Picture ON;
        INSERT INTO dbo.Picture
        (
            Id, MimeType, SeoFilename, AltAttribute, TitleAttribute, IsNew, VirtualPath
        )
        SELECT
            p.Id, p.MimeType, p.SeoFilename, p.AltAttribute, p.TitleAttribute, p.IsNew, p.VirtualPath
        FROM [HoodArcheryShopV480_OLD_yedekeski].dbo.Picture p
        JOIN #NeededPictures np ON np.PictureId = p.Id
        WHERE NOT EXISTS (SELECT 1 FROM dbo.Picture x WHERE x.Id = p.Id);
        SET IDENTITY_INSERT dbo.Picture OFF;
    END;

    -- PictureBinary: nopCommerce sürümüne göre Id kolonlu/kolonsuz olabilir; dinamik kopyala.
    IF OBJECT_ID(N'dbo.PictureBinary', N'U') IS NOT NULL
       AND OBJECT_ID(N'HoodArcheryShopV480_OLD_yedekeski.dbo.PictureBinary', N'U') IS NOT NULL
    BEGIN
        DECLARE @pbHasId bit = CASE WHEN COL_LENGTH('dbo.PictureBinary', 'Id') IS NULL THEN 0 ELSE 1 END;

        IF @pbHasId = 1
        BEGIN
            SET IDENTITY_INSERT dbo.PictureBinary ON;
            INSERT INTO dbo.PictureBinary (Id, PictureId, BinaryData)
            SELECT pb.Id, pb.PictureId, pb.BinaryData
            FROM [HoodArcheryShopV480_OLD_yedekeski].dbo.PictureBinary pb
            JOIN #NeededPictures np ON np.PictureId = pb.PictureId
            WHERE NOT EXISTS (SELECT 1 FROM dbo.PictureBinary x WHERE x.Id = pb.Id)
              AND NOT EXISTS (SELECT 1 FROM dbo.PictureBinary x WHERE x.PictureId = pb.PictureId);
            SET IDENTITY_INSERT dbo.PictureBinary OFF;
        END
        ELSE
        BEGIN
            INSERT INTO dbo.PictureBinary (PictureId, BinaryData)
            SELECT pb.PictureId, pb.BinaryData
            FROM [HoodArcheryShopV480_OLD_yedekeski].dbo.PictureBinary pb
            JOIN #NeededPictures np ON np.PictureId = pb.PictureId
            WHERE NOT EXISTS (SELECT 1 FROM dbo.PictureBinary x WHERE x.PictureId = pb.PictureId);
        END
    END;

    -- Video ana kayıtları: varsa Id korunmalı.
    IF OBJECT_ID(N'dbo.ProductReviewVideo', N'U') IS NOT NULL
       AND OBJECT_ID(N'HoodArcheryShopV480_OLD_yedekeski.dbo.ProductReviewVideo', N'U') IS NOT NULL
       AND EXISTS (SELECT 1 FROM #NeededVideos nv WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductReviewVideo v WHERE v.Id = nv.VideoId))
    BEGIN
        SET IDENTITY_INSERT dbo.ProductReviewVideo ON;
        INSERT INTO dbo.ProductReviewVideo
        (
            Id, MimeType, SeoFilename, AltAttribute, TitleAttribute, IsNew, VirtualPath
        )
        SELECT
            v.Id, v.MimeType, v.SeoFilename, v.AltAttribute, v.TitleAttribute, v.IsNew, v.VirtualPath
        FROM [HoodArcheryShopV480_OLD_yedekeski].dbo.ProductReviewVideo v
        JOIN #NeededVideos nv ON nv.VideoId = v.Id
        WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductReviewVideo x WHERE x.Id = v.Id);
        SET IDENTITY_INSERT dbo.ProductReviewVideo OFF;
    END;

    IF OBJECT_ID(N'dbo.ProductReviewVideoBinary', N'U') IS NOT NULL
       AND OBJECT_ID(N'HoodArcheryShopV480_OLD_yedekeski.dbo.ProductReviewVideoBinary', N'U') IS NOT NULL
    BEGIN
        SET IDENTITY_INSERT dbo.ProductReviewVideoBinary ON;
        INSERT INTO dbo.ProductReviewVideoBinary (Id, BinaryData, VideoId)
        SELECT vb.Id, vb.BinaryData, vb.VideoId
        FROM [HoodArcheryShopV480_OLD_yedekeski].dbo.ProductReviewVideoBinary vb
        JOIN #NeededVideos nv ON nv.VideoId = vb.VideoId
        WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductReviewVideoBinary x WHERE x.Id = vb.Id)
          AND NOT EXISTS (SELECT 1 FROM dbo.ProductReviewVideoBinary x WHERE x.VideoId = vb.VideoId);
        SET IDENTITY_INSERT dbo.ProductReviewVideoBinary OFF;
    END;

    -- Mapping kayıtları: Id korunmasına gerek yok, duplicate önlenir.
    INSERT INTO dbo.CustomProductReviewMapping
    (
        ProductReviewId,
        PictureId,
        ProductReviewVideoId,
        DisplayOrder
    )
    SELECT
        sm.ProductReviewId,
        sm.PictureId,
        sm.ProductReviewVideoId,
        sm.DisplayOrder
    FROM #SourceMappings sm
    WHERE (sm.PictureId IS NULL OR EXISTS (SELECT 1 FROM dbo.Picture p WHERE p.Id = sm.PictureId))
      AND (sm.ProductReviewVideoId IS NULL OR EXISTS (SELECT 1 FROM dbo.ProductReviewVideo v WHERE v.Id = sm.ProductReviewVideoId))
      AND NOT EXISTS (
          SELECT 1
          FROM dbo.CustomProductReviewMapping x
          WHERE x.ProductReviewId = sm.ProductReviewId
            AND ISNULL(x.PictureId, -1) = ISNULL(sm.PictureId, -1)
            AND ISNULL(x.ProductReviewVideoId, -1) = ISNULL(sm.ProductReviewVideoId, -1)
      );

    SELECT 'Source custom media mappings prepared' AS Info, COUNT(*) AS Cnt FROM #SourceMappings
    UNION ALL SELECT 'Needed pictures', COUNT(*) FROM #NeededPictures
    UNION ALL SELECT 'Needed videos', COUNT(*) FROM #NeededVideos
    UNION ALL SELECT 'CustomProductReviewMapping count after transaction', COUNT(*) FROM dbo.CustomProductReviewMapping;

    SELECT TOP (100)
        sm.ProductReviewId,
        sm.PictureId,
        sm.ProductReviewVideoId,
        pr.ProductId,
        pr.Title,
        pr.CreatedOnUtc
    FROM #SourceMappings sm
    JOIN dbo.ProductReview pr ON pr.Id = sm.ProductReviewId
    ORDER BY pr.CreatedOnUtc DESC;

    IF @Commit = 1
    BEGIN
        IF OBJECT_ID(N'dbo.Picture', N'U') IS NOT NULL DBCC CHECKIDENT (N'dbo.Picture', RESEED);
        IF OBJECT_ID(N'dbo.PictureBinary', N'U') IS NOT NULL AND COL_LENGTH('dbo.PictureBinary', 'Id') IS NOT NULL DBCC CHECKIDENT (N'dbo.PictureBinary', RESEED);
        IF OBJECT_ID(N'dbo.ProductReviewVideo', N'U') IS NOT NULL DBCC CHECKIDENT (N'dbo.ProductReviewVideo', RESEED);
        IF OBJECT_ID(N'dbo.ProductReviewVideoBinary', N'U') IS NOT NULL DBCC CHECKIDENT (N'dbo.ProductReviewVideoBinary', RESEED);
        IF OBJECT_ID(N'dbo.CustomProductReviewMapping', N'U') IS NOT NULL DBCC CHECKIDENT (N'dbo.CustomProductReviewMapping', RESEED);

        COMMIT;
        SELECT 'COMMIT OK - CustomProductReviewMapping media restored.' AS Result;
    END
    ELSE
    BEGIN
        ROLLBACK;
        SELECT 'DRY RUN OK - rollback. Sonuc dogruysa @Commit = 1 yap.' AS Result;
    END
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    BEGIN TRY SET IDENTITY_INSERT dbo.Picture OFF; END TRY BEGIN CATCH END CATCH;
    BEGIN TRY SET IDENTITY_INSERT dbo.PictureBinary OFF; END TRY BEGIN CATCH END CATCH;
    BEGIN TRY SET IDENTITY_INSERT dbo.ProductReviewVideo OFF; END TRY BEGIN CATCH END CATCH;
    BEGIN TRY SET IDENTITY_INSERT dbo.ProductReviewVideoBinary OFF; END TRY BEGIN CATCH END CATCH;
    THROW;
END CATCH;
