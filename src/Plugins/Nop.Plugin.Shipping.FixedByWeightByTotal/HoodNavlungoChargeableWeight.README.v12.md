# Hood/Navlungo Chargeable Weight - v12

## What changed

1. Navlungo Learning can now show **products only**.
   - Use the `products only` checkbox on `/Admin/FixedByWeightByTotal/NavlungoLearning`.
   - Click a product row to expand its learned shipment/package details.

2. Product-level profile apply was added.
   - Each product row has a profile dropdown:
     - PRODUCT_DEFAULT
     - ATTRIBUTE_VALUE
     - ATTRIBUTE_COMBINATION
     - ARROW_PCS
     - ALL
   - `Apply profile` applies only that product with the selected profile.

3. Tooltips were added.
   - Buttons and profile types now explain what they do on hover.

## Profile behavior

- PRODUCT_DEFAULT: creates one max/safe package rule for the whole product and updates native Product Weight/Length/Width/Height.
- ATTRIBUTE_VALUE: applies rules based on selected shipping-driver attribute values.
- ATTRIBUTE_COMBINATION: applies exact full variation-combination rules.
- ARROW_PCS: applies Pcs-based arrow rules.
- ALL: applies all learned rule types for the selected product.

## Recommended workflow

1. Open `/Admin/FixedByWeightByTotal/NavlungoLearning?minimumSampleCount=1&groupByProduct=true`.
2. Click product row to inspect its details.
3. For armor/costume sets, open `Rules / driver` and set the shipping-driver attribute, e.g. `Armor Sets`.
4. Return to Learning page and apply the product profile as `ATTRIBUTE_VALUE`.
5. Use `ATTRIBUTE_COMBINATION` only when more than one attribute changes package dimensions.
6. Use `PRODUCT_DEFAULT` for products without meaningful shipping attributes.
