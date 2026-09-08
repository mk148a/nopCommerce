# v20 - Production Time UI polish + bulk extraction

Changes:
- Public product page card is more compact and Element-theme friendly.
- Replaced sprite icon with an inline SVG handmade/craft icon.
- Removed duplicate customer-facing message; the card now clearly says shipping starts after production.
- Added bulk extraction tools to `/Admin/FixedByWeightByTotal/ProductProductionTimes`.
- Bulk extraction scans product descriptions and creates `HoodProductProductionTime` records automatically.

Bulk manager:
- Max products: limits how many products are scanned in one run.
- Only products without record: prevents overwriting manually edited records.
- Published products only: skips unpublished/deleted products.

Install:
1. Copy over `src/Plugins/Nop.Plugin.Shipping.FixedByWeightByTotal`.
2. Build the plugin project.
3. Restart IIS / recycle app pool.
