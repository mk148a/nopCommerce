# Hood/Navlungo chargeable weight v5

Changes:

1. ProductAttributeValue edit popup now has a Hood/Navlungo shipping dimensions block through `AdminWidgetZones.ProductAttributeValueDetailsBottom`.
2. Attribute value fields save to `HoodProductShippingDimensionRule` with `RuleType=ATTRIBUTE_VALUE` or `ARROW_PCS`.
3. Navlungo tracking links use `https://navlungo.com/track?carrier=nvl&trackingNumber=...` instead of 17track for NVL numbers.
4. Shipment links page always shows direct Navlungo public tracking when `NavlungoTrackingNo` exists.
5. The manual SQL includes the missing `HoodProductShippingDimensionExclusion` table; run it once if the exclusions page throws SQL error 208.

Important immediate SQL for existing installs:

```sql
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
```
