using Microsoft.AspNetCore.Mvc;
using Nop.Web.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Misc.HoodSitemapCompatibility.Controllers;

/// <summary>
/// Handles an obsolete partition URL emitted by a previous sitemap layout.
/// </summary>
public sealed class LegacySitemapController : BasePublicController
{
    [AcceptVerbs("GET", "HEAD")]
    [CheckLanguageSeoCode(true)]
    public IActionResult LegacyPartition()
    {
        return RedirectToCanonicalSitemap();
    }

    [AcceptVerbs("GET", "HEAD")]
    [CheckLanguageSeoCode(true)]
    public IActionResult SitemapNine()
    {
        // The current sitemap has one valid partition. Redirecting is preferable to
        // triggering nopCommerce's invalid-partition physical-file exception.
        // Use the canonical absolute URL. The storefront's language redirect filter
        // rewrites relative redirects (for example to /en/sitemap-9.xml), which would
        // send crawlers straight back to an invalid legacy route.
        return RedirectToCanonicalSitemap();
    }

    private IActionResult RedirectToCanonicalSitemap()
    {
        // Use an absolute URL: the language redirect filter rewrites relative
        // redirects and can otherwise send crawlers back to a legacy route.
        return RedirectPermanent("https://hoodarcheryshop.com/sitemap.xml");
    }
}
