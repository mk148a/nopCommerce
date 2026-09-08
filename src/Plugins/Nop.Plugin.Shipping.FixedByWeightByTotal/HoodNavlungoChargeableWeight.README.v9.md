# v9 - nopCommerce 4.80 ProductAttributeMapping table fix

Fixes NavlungoLearning 500 error caused by raw SQL using `dbo.ProductAttributeMapping`.

nopCommerce database table name is `dbo.Product_ProductAttribute_Mapping`, while the EF/domain type is `ProductAttributeMapping`.

Changed only the ARROW_PCS learning SQL join:

```sql
JOIN dbo.Product_ProductAttribute_Mapping PAM
    ON PAM.Id = PAV.ProductAttributeMappingId
```

Install over v8, rebuild, restart IIS/AppPool.
