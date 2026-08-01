# v14 - Product default billable weight fix

Problem: some products have almost constant actual weight but a much larger dimensional/volumetric weight. If these are applied as PRODUCT_DEFAULT and any native FixedByWeight path reads only Product.Weight, the dimensional weight can be ignored.

Fixes:

1. PRODUCT_DEFAULT application writes native Product.Weight as effective/billable grams:
   max(actual weight grams, Navlungo chargeable kg * 1000, L*W*H/divisor*1000).

2. PRODUCT_DEFAULT profile selection uses the exact package dimensions from the shipment with the highest chargeable kg. It no longer combines max length from one shipment with max width/height from another shipment.

3. Runtime chargeable calculation still uses:
   max(WeightGram/1000, LengthCm*WidthCm*HeightCm/Divisor).

Use PRODUCT_DEFAULT for products where you want one safe package profile for the whole product. Use ATTRIBUTE_VALUE for products where a driver attribute like Armor Set, Pcs, Quiver Set changes the package.
