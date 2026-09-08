# Hood/Navlungo Chargeable Weight v8

Adds Navlungo Learning support for ARROW_PCS / ProductAttributeValue rules.

## What changed

- Learning page now returns two suggestion layers:
  - ATTRIBUTE_COMBINATION from `dbo.NavlungoProductMeasureStatsSafe`
  - ARROW_PCS from trusted matched shipments by parsing `OrderItem.AttributesXml` and Pcs attribute value IDs
- Learning page includes separate apply buttons:
  - Apply all suggestions
  - Apply ARROW_PCS / Pcs rules
  - Apply combination rules
- ARROW_PCS suggestions write directly to `HoodProductShippingDimensionRule.ProductAttributeValueId`, so ProductAttributeValueEditPopup fields auto-fill.
- ARROW_PCS rules are created with `IsShipSeparately = true` by default.

## Required analysis tables

The ARROW_PCS learning depends on these existing tables from your Navlungo import/matching workflow:

- `dbo.NavlungoProductMeasureStatsSafe`
- `dbo.NavlungoOrderMatchFinal`
- `dbo.NavlungoPackages_20260515`

## Recommended use

1. Open `/Admin/FixedByWeightByTotal/NavlungoLearning?minimumSampleCount=2`
2. Review ARROW_PCS rows.
3. Click `Apply ARROW_PCS / Pcs rules`.
4. Open a Pcs attribute value popup; L/W/H + weight fields should be filled automatically.
