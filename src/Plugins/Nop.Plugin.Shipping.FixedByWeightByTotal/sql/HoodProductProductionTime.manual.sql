/*
  HoodProductProductionTime table repair / manual migration
  Run this ONCE in the nopCommerce database: HoodArcheryShopV480bugfixLancelotDb
*/

USE [HoodArcheryShopV480bugfixLancelotDb];
GO

IF OBJECT_ID(N'dbo.HoodProductProductionTime', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[HoodProductProductionTime]
    (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_HoodProductProductionTime] PRIMARY KEY,
        [ProductId] INT NOT NULL,
        [Enabled] BIT NOT NULL CONSTRAINT [DF_HoodProductProductionTime_Enabled] DEFAULT(1),
        [IsHandmade] BIT NOT NULL CONSTRAINT [DF_HoodProductProductionTime_IsHandmade] DEFAULT(1),
        [ProductionMinDays] INT NOT NULL CONSTRAINT [DF_HoodProductProductionTime_ProductionMinDays] DEFAULT(0),
        [ProductionMaxDays] INT NOT NULL CONSTRAINT [DF_HoodProductProductionTime_ProductionMaxDays] DEFAULT(0),
        [ProductionTimeText] NVARCHAR(400) NULL,
        [Message] NVARCHAR(1000) NULL,
        [DisplayOnProductPage] BIT NOT NULL CONSTRAINT [DF_HoodProductProductionTime_DisplayOnProductPage] DEFAULT(1),
        [IncludeInShippingEstimate] BIT NOT NULL CONSTRAINT [DF_HoodProductProductionTime_IncludeInShippingEstimate] DEFAULT(1),
        [ParsedFromDescription] BIT NOT NULL CONSTRAINT [DF_HoodProductProductionTime_ParsedFromDescription] DEFAULT(0),
        [Source] NVARCHAR(200) NULL,
        [CreatedOnUtc] DATETIME2(7) NOT NULL CONSTRAINT [DF_HoodProductProductionTime_CreatedOnUtc] DEFAULT(SYSUTCDATETIME()),
        [UpdatedOnUtc] DATETIME2(7) NULL
    );

    CREATE INDEX [IX_HoodProductProductionTime_ProductId] ON [dbo].[HoodProductProductionTime]([ProductId]);
    CREATE INDEX [IX_HoodProductProductionTime_Enabled_ProductId] ON [dbo].[HoodProductProductionTime]([Enabled], [ProductId]);
END
GO

SELECT
    CASE WHEN OBJECT_ID(N'dbo.HoodProductProductionTime', N'U') IS NULL THEN 'MISSING' ELSE 'OK' END AS HoodProductProductionTimeStatus,
    COUNT_BIG(*) AS RowCount
FROM [dbo].[HoodProductProductionTime];
GO
