# Hood/Navlungo Chargeable Weight v4

Adds:
- Shipment/consignment links page and admin shipment detail widget button.
- Learning detail document/package links.
- Category bulk apply from source product to category products.
- Product automation exclusions.
- Existing checkout computation remains chargeable-weight based: max(actual kg, L*W*H/divisor) then multiplied to existing gram-based FixedByWeightByTotal rate lookup.

Admin URLs:
- /Admin/FixedByWeightByTotal/NavlungoLearning
- /Admin/FixedByWeightByTotal/NavlungoShipmentLinks
- /Admin/FixedByWeightByTotal/CategoryBulkApplyDimensions
- /Admin/FixedByWeightByTotal/ShippingDimensionExclusions
- /Admin/FixedByWeightByTotal/ShippingDimensionRules?productId=253
