# v24 - Live PTT Post service

This version adds an optional live PTT international parcel option named **Post service**.

## What it does

- Keeps existing UPS/FedEx/THY/Navlungo rate-table options.
- Adds an extra Post service option only when all non-free-shipping cart items are explicitly eligible.
- Eligibility can be configured by ProductId CSV and/or CategoryId CSV on the plugin Configure page.
- Calls the live PTT endpoint `https://api.ptt.gov.tr/api/DeliveryFee/getAbroadDetailed`.
- Uses actual weight only for the PTT calculation and sends `desi = 0`.
- Uses product/rule dimensions only as guards. If a product exceeds configured max side or girth limits, Post service is hidden.
- Adds production time to the PTT delivery date estimate just like the other methods.

## Default PTT request values

```json
{
  "DeliveryKind": "YD KOLİ",
  "DistributionType": "UC",
  "additionalService": "GM",
  "desi": 0
}
```

The destination country is taken from nopCommerce country `TwoLetterIsoCode`.

## Important currency note

The plugin uses `HoodPttLivePriceMultiplier` to convert the live PTT response amount into the store currency.
If PTT returns TRY and the store rate table is USD, set this multiplier to your TRY→USD conversion factor.

## Configuration page

`/Admin/FixedByWeightByTotal/Configure`

New card: **PTT live Post service**.
