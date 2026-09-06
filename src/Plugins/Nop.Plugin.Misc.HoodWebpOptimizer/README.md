# Hood Adaptive WebP Optimizer

This nopCommerce 4.80 plugin replaces `IPictureService` only when the active deployment uses local `PictureService`. It deliberately does **not** override `AzurePictureService`; Azure requires a storage-aware adapter and separate validation.

## Upload behaviour

- Eligible JPEG, PNG, BMP and TIFF uploads are encoded as several WebP candidates.
- A candidate is accepted only if it achieves the configured byte saving and remains below the configured sampled normalized RGBA mean-squared-error threshold.
- SVG, GIF, animated images, existing WebP, failed decodes and no-size-win inputs remain exactly as uploaded.
- When enabled, source uploads are copied outside `wwwroot` to `App_Data/HoodWebpOptimizer/originals/{PictureId}` with a small MIME manifest. Administrators can retrieve an original through the protected admin action.

When a converted update replaces a filesystem-backed picture, the now-unreferenced old source extension is removed only after `PictureService` has saved the new WebP. Thumbnail regeneration is still performed by the normal nopCommerce service.

The sampled error gate is deliberately described as **SSIM-like**, not SSIM: it samples 64x64 pixels and detects encoding drift deterministically. It cannot replace content-aware human review for logos, text-heavy graphics or legal artwork; those formats are left unchanged when the gate has no safe compression win.

## Legacy processing

New uploads are processed synchronously by the local media service decorator. Existing catalog images are optional, opt-in, and processed in bounded batches (20 by default every 15 minutes), preventing bulk conversion from monopolising IIS CPU or disk. A monotonic Picture ID cursor means an image with no safe WebP win is not retried forever.

## Deployment prerequisites

1. Build and stage the plugin. Confirm the production store does not use `AzureBlobConfig:ConnectionString`.
2. Install from Admin > Configuration > Local plugins and open the plugin configuration page.
3. Upload a JPEG and PNG representative image; verify `Picture.MimeType`, image URL extension, thumbnail regeneration, visual quality, and protected original download.
4. Run one legacy batch and verify a product page and admin media picker before enabling scheduled backfill.

No existing media is changed by installation alone because legacy processing is disabled by default.
