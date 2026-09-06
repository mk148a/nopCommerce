# v11 - Shipping driver attributes

This version adds product-level shipping driver attributes.

Use case:
- Arrows: `Pcs` controls shipping dimensions.
- Armor/costume sets: `Armor Sets` controls shipping dimensions.

Workflow:
1. Open `/Admin/FixedByWeightByTotal/ShippingDimensionRules?productId=PRODUCT_ID`.
2. In **Shipping driver attributes**, choose the attribute that controls shipping.
3. Select `ATTRIBUTE_VALUE` for general attributes or `ARROW_PCS` for arrow pcs.
4. Save.
5. Open Navlungo Learning and click `Apply selected shipping-driver attribute rules`.

The Learning page will then create one rule per selected attribute value instead of only exact full-combination rules.
