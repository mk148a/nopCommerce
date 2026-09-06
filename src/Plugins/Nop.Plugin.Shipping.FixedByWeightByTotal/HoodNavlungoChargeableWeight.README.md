# Hood/Navlungo chargeable-weight plugin preparation

This package extends `Shipping.FixedByWeightByTotal` with:

1. Product/attribute shipping dimension rules.
2. Chargeable-weight calculation before the existing FixedByWeightByTotal rate lookup.
3. Admin pages:
   - `/Admin/FixedByWeightByTotal/ShippingDimensionRules`
   - `/Admin/FixedByWeightByTotal/NavlungoLearning`
4. Settings on the plugin Configure page:
   - Enable chargeable weight
   - Dimensional divisor, default `5000`
   - Rate weight multiplier, default `1000` for gram-based rate tables

## Calculation

For each cart item:

```text
actualWeightKg = WeightGram / 1000
volumetricWeightKg = LengthCm * WidthCm * HeightCm / Divisor
chargeableWeightKg = max(actualWeightKg, volumetricWeightKg)
rateLookupWeight = chargeableWeightKg * RateWeightMultiplier * Quantity
```

With the current Hood store setup, product weights and FixedByWeightByTotal rate bands are gram-based, so `RateWeightMultiplier = 1000`.

## Rule lookup priority

1. `ProductId + AttributeHash` (`ATTRIBUTE_COMBINATION`)
2. `ProductId + ProductAttributeValueId` (`ARROW_PCS`)
3. `ProductId + ProductAttributeValueId` (`ATTRIBUTE_VALUE`)
4. `ProductId + PRODUCT_DEFAULT`
5. Native nopCommerce product dimensions + product weight + attribute weight adjustments

## Learning page

The learning page reads `dbo.NavlungoProductMeasureStatsSafe` created by the SQL matching workflow. If the table does not exist, the page shows no suggestions.

Use:

```text
/Admin/FixedByWeightByTotal/NavlungoLearning?minimumSampleCount=2
```

Then click `Apply suggestions`. Suggestions create `HoodProductShippingDimensionRule` rows.

## Manual dimension rules

Use:

```text
/Admin/FixedByWeightByTotal/ShippingDimensionRules?productId=253
```

Examples for arrow Pcs rules:

```text
RuleType = ARROW_PCS
ProductAttributeValueId = Pcs value id
LengthCm = 84
WidthCm = 5 / 6 / 8
HeightCm = 5 / 6 / 8
WeightGram = 400 / 600 / 1000
Divisor = 5000
PackageCount = 1
```

## Important

This is a source package. Put it under your nopCommerce 4.80 source tree at:

```text
src/Plugins/Nop.Plugin.Shipping.FixedByWeightByTotal
```

Then build from the nopCommerce solution so `$(SolutionDir)` resolves correctly.

## v2 - Learning Details button

Added a `Details` button on the Navlungo Shipping Learning page.

Admin URLs:

- `/Admin/FixedByWeightByTotal/NavlungoLearning`
- `/Admin/FixedByWeightByTotal/NavlungoLearningDetails?productId=253&attributeHash=...&ruleType=ATTRIBUTE_COMBINATION`

The details page reconstructs the exact trusted sample set used by `dbo.NavlungoProductMeasureStatsSafe`:

- `NavlungoOrderMatchFinal`
- `OrderItem`
- `NavlungoPackages_20260515`
- safe match decisions only
- single order line
- quantity 1
- single package

The page shows shipment, order, receiver, date, dimensions, real weight, chargeable weight, match score and attribute description.
