# Hood/Navlungo v15 - Handmade Production Time

This version adds product-level handmade / make-to-order production lead time.

## What it does

- Stores per-product production time in `dbo.HoodProductProductionTime`.
- Adds an admin product edit widget/card for production time.
- Adds a public product page box: "Handmade production time".
- Adds production max days to shipping method `TransitDays`, so product-page and checkout estimates use production + carrier transit.
- Adds an admin manager: `/Admin/FixedByWeightByTotal/ProductProductionTimes`.
- Uses typed, manually maintained minimum and maximum production-day values. It never derives production time from product-description text.

## Recommended workflow

1. Open product edit page.
2. Use the `Hood production time` card.
3. Enter and review the minimum and maximum production days from the confirmed production schedule.
4. Save.

## Manual SQL

If migration does not run automatically, execute the V15 section in:

`sql/HoodNavlungoDimensionRules.manual.sql`
