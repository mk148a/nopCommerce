# Hood/Navlungo v15 - Handmade Production Time

This version adds product-level handmade / make-to-order production lead time.

## What it does

- Stores per-product production time in `dbo.HoodProductProductionTime`.
- Adds an admin product edit widget/card for production time.
- Adds a public product page box: "Handmade production time".
- Adds production max days to shipping method `TransitDays`, so product-page and checkout estimates use production + carrier transit.
- Adds an admin manager: `/Admin/FixedByWeightByTotal/ProductProductionTimes`.
- Adds extraction from product description. It looks for phrases such as `Production Time`, `Processing Time`, `made to order`, `handmade`, `about 1 week`, `2-4 weeks`, etc. It avoids treating pure shipping phrases like `delivery 3-5 days` as production time unless production/made/processing is mentioned nearby.

## Recommended workflow

1. Open product edit page.
2. Use the `Hood production time` card.
3. Click `Extract from description`.
4. Review min/max production days.
5. Save.

## Manual SQL

If migration does not run automatically, execute the V15 section in:

`sql/HoodNavlungoDimensionRules.manual.sql`
