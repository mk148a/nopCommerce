# Stripe + Apple Pay/Google Pay compatibility bundle

This build produces two nopCommerce payment plugins for each source version in
the matrix. nopCommerce plugin binaries are tied to the host version; a single
DLL cannot truthfully support every major/minor release. Install only the pair
under the matching `Plugins` folder.

Supported and verified in this bundle:

| nopCommerce | target framework | card plugin | Apple Pay/Google Pay plugin |
| --- | --- | --- | --- |
| 4.50 | net6.0 | Stripe 1.25 | StripeApplePay 1.07 |
| 4.60 | net7.0 | Stripe 1.25 | StripeApplePay 1.07 |
| 4.70 | net8.0 | Stripe 1.25 | StripeApplePay 1.07 |
| 4.80 | net9.0 | Stripe 1.25 | StripeApplePay 1.07 |

`Stripe.net` is pinned to 52.2.0 in every build. The compatibility shims are
limited to nopCommerce API differences (`IPaymentMethod` view-component type,
admin area constant, and nullable-string helpers). Product, order, wallet,
checkout, and database data are not changed by packaging.

This is the requested unlicensed build. A source audit found no Genius license
service, activation key, license HTTP call, or license assembly in either
plugin. The output is therefore not a license bypass; it contains no license
control at all. License enforcement can be added later as a separate product
variant without changing this package.

Build from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\StripeCompatibility\Build-StripeCompatibilityBundle.ps1
```

The script requires the refs listed in `compatibility-matrix.json`, runs restore
and release builds for both plugins, filters the host application's assemblies
out of each plugin folder, writes `compatibility-manifest.json`, and creates a
zip beside the output directory. It does not deploy or alter the live site.

Validate an extracted bundle or the zip itself:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\StripeCompatibility\Test-StripeCompatibilityBundle.ps1 `
  -BundleRoot C:\path\to\stripe-compatibility-bundle.zip
```
