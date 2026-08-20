/* Manual fallback if nopCommerce migration does not create the table automatically. */
IF OBJECT_ID(N'dbo.HoodProductShippingDimensionRule', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HoodProductShippingDimensionRule
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_HoodProductShippingDimensionRule PRIMARY KEY,
        ProductId INT NOT NULL,
        ProductAttributeValueId INT NULL,
        AttributeValueIdsCsv NVARCHAR(500) NULL,
        AttributeHash NVARCHAR(64) NULL,
        RuleType NVARCHAR(50) NULL,
        LengthCm DECIMAL(18,4) NOT NULL,
        WidthCm DECIMAL(18,4) NOT NULL,
        HeightCm DECIMAL(18,4) NOT NULL,
        WeightGram DECIMAL(18,4) NULL,
        Divisor DECIMAL(18,4) NOT NULL DEFAULT 5000,
        PackageCount INT NOT NULL DEFAULT 1,
        IsShipSeparately BIT NOT NULL DEFAULT 0,
        IsActive BIT NOT NULL DEFAULT 1,
        SampleCount INT NOT NULL DEFAULT 0,
        Confidence NVARCHAR(50) NULL,
        Source NVARCHAR(200) NULL,
        ExampleShipmentNo NVARCHAR(50) NULL,
        ExampleOrderId INT NULL,
        CreatedOnUtc DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedOnUtc DATETIME2(0) NULL
    );

    CREATE INDEX IX_HoodProductShippingDimensionRule_Product
        ON dbo.HoodProductShippingDimensionRule(ProductId, IsActive);
    CREATE INDEX IX_HoodProductShippingDimensionRule_Value
        ON dbo.HoodProductShippingDimensionRule(ProductAttributeValueId, IsActive);
    CREATE INDEX IX_HoodProductShippingDimensionRule_Hash
        ON dbo.HoodProductShippingDimensionRule(ProductId, AttributeHash, IsActive);
END
GO

-- v4: products excluded from Navlungo Learning and category bulk apply operations.
IF OBJECT_ID(N'dbo.HoodProductShippingDimensionExclusion', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HoodProductShippingDimensionExclusion
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ProductId INT NOT NULL,
        Reason NVARCHAR(1000) NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedOnUtc DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedOnUtc DATETIME2(0) NULL
    );

    CREATE INDEX IX_HoodProductShippingDimensionExclusion_Product
    ON dbo.HoodProductShippingDimensionExclusion(ProductId, IsActive);
END;
GO

-- Optional helper functions used by historical Navlungo matching queries.
IF OBJECT_ID(N'dbo.fnOnlyAlphaNumUpper', N'FN') IS NULL
    EXEC(N'
CREATE FUNCTION dbo.fnOnlyAlphaNumUpper(@s NVARCHAR(MAX))
RETURNS NVARCHAR(4000)
AS
BEGIN
    DECLARE @i INT = 1;
    DECLARE @r NVARCHAR(4000) = N'''';
    DECLARE @ch NCHAR(1);
    SET @s = UPPER(ISNULL(@s, N''''));
    WHILE @i <= LEN(@s)
    BEGIN
        SET @ch = SUBSTRING(@s, @i, 1);
        IF @ch LIKE N''[A-Z0-9]'' SET @r += @ch;
        SET @i += 1;
    END
    RETURN @r;
END');
GO

------------------------------------------------------------
-- v11 - Shipping driver attribute table
-- Choose which product attribute controls package dimensions/weight.
-- Example: arrows=Pcs, armor=Armor Sets.
------------------------------------------------------------
IF OBJECT_ID(N'dbo.HoodProductShippingDriverAttribute', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HoodProductShippingDriverAttribute
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ProductId INT NOT NULL,
        ProductAttributeMappingId INT NOT NULL,
        ProductAttributeId INT NOT NULL,
        ProductAttributeName NVARCHAR(400) NULL,
        RuleType NVARCHAR(50) NOT NULL DEFAULT N'ATTRIBUTE_VALUE',
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedOnUtc DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedOnUtc DATETIME2(0) NULL
    );

    CREATE INDEX IX_HoodProductShippingDriverAttribute_Product
    ON dbo.HoodProductShippingDriverAttribute(ProductId, IsActive);

    CREATE INDEX IX_HoodProductShippingDriverAttribute_Mapping
    ON dbo.HoodProductShippingDriverAttribute(ProductAttributeMappingId, IsActive);
END;
GO

------------------------------------------------------------
-- V15 - handmade / make-to-order production time
------------------------------------------------------------
IF OBJECT_ID(N'dbo.HoodProductProductionTime', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HoodProductProductionTime
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ProductId INT NOT NULL,
        Enabled BIT NOT NULL DEFAULT 1,
        IsHandmade BIT NOT NULL DEFAULT 1,
        ProductionMinDays INT NOT NULL DEFAULT 0,
        ProductionMaxDays INT NOT NULL DEFAULT 0,
        ProductionTimeText NVARCHAR(400) NULL,
        Message NVARCHAR(1000) NULL,
        DisplayOnProductPage BIT NOT NULL DEFAULT 1,
        IncludeInShippingEstimate BIT NOT NULL DEFAULT 1,
        ParsedFromDescription BIT NOT NULL DEFAULT 0,
        Source NVARCHAR(200) NULL,
        CreatedOnUtc DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedOnUtc DATETIME2(7) NULL
    );

    CREATE INDEX IX_HoodProductProductionTime_Product
        ON dbo.HoodProductProductionTime(ProductId, Enabled);
END;
GO
