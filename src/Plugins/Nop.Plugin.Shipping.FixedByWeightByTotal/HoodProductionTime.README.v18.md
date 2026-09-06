# v18 - Element theme production time layout fix

- Public production time widget now renders only in `ProductDetailsOverviewBottom`, inside Element theme `.overview` column.
- Removed public rendering from `ProductDetailsEssentialBottom` and `ProductDetailsBeforeCollateral` because those zones are outside the overview column in the Element product template and can break the gallery/overview float layout.
- Component view now uses theme-scoped CSS under `.product-details-page .overview .hood-production-time-card`.
- A tiny script moves the card after `.overview .delivery` when the Element theme delivery block exists; otherwise it remains safely at overview bottom.
- No SQL change.
