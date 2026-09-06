# v22 - Checkout shipping method labels and delivery estimate

Changes:

- Public checkout/shipping option names no longer show the `Navlungo` prefix.
  - Example: `NAVLUNGO UPS EXPRESS` -> `UPS Express`
  - Example: `NAVLUNGO FEDEX EXPRESS` -> `FedEx Express`
- Shipping option descriptions now show carrier transit time, production time, and estimated delivery time.
- Estimated delivery uses the longest production-time rule among the cart items.
- `ShippingOption.TransitDays` still receives carrier transit + production max days, so nopCommerce date calculations use the combined estimate.

If your theme does not display `ShippingOption.Description` below each method, update the checkout shipping method view to render option.Description.
