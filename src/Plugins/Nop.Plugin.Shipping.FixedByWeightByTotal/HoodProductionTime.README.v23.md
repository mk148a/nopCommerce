# v23 - Estimated delivery date

Changes:

- Checkout shipping option description now shows `Estimated delivery date` instead of `Estimated delivery time`.
- The value is a real date/date range calculated from today + longest production time in the cart + carrier transit days.
- Product page production card also shows `Estimated delivery date` as a date range.
- Public method name cleanup from v22 is preserved: `NAVLUNGO UPS EXPRESS` -> `UPS Express`.

Example:

```text
UPS Express ($129.92)
Transit time: 3 days
Production time: 14-21 days
Estimated delivery date: Jun 10 - Jun 17, 2026
Shipping starts after production.
```

Notes:

- Date formatting uses the current UI culture (`CultureInfo.CurrentUICulture`).
- Baseline is the web server local date (`DateTime.Today`).
