# Hood/Navlungo Chargeable Weight - v10

Changes:

1. Navlungo Learning now creates PRODUCT_DEFAULT suggestions for products without product attributes, or rows whose attribute hash is the empty SHA-256 hash.
2. Applying PRODUCT_DEFAULT rules also updates the native nopCommerce Product shipping fields: Weight, Length, Width, Height, and ShipSeparately.
3. The cart calculator now matches ATTRIBUTE_COMBINATION rules by either raw AttributesXml hash or selected ProductAttributeValueId CSV hash.
4. Empty ATTRIBUTE_COMBINATION hash rules are ignored at runtime so no-attribute products fall through to PRODUCT_DEFAULT.
5. PackageCount is now included in rate lookup weight: max(actual kg, volumetric kg) x PackageCount x Quantity x multiplier.
6. Navlungo Learning UI has a new button: Apply product default / no-attribute rules.
