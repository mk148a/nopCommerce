using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Nop.Core;
using Nop.Core.Domain;
using Nop.Services.Media;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Components;

/// <summary>
/// Emits the store-level structured-data graph once, on the public home page.
/// </summary>
public class HoodOnlineStoreJsonLdViewComponent : NopViewComponent
{
    private readonly IStoreContext _storeContext;
    private readonly StoreInformationSettings _storeInformationSettings;
    private readonly IPictureService _pictureService;

    public HoodOnlineStoreJsonLdViewComponent(IStoreContext storeContext,
        StoreInformationSettings storeInformationSettings,
        IPictureService pictureService)
    {
        _storeContext = storeContext;
        _storeInformationSettings = storeInformationSettings;
        _pictureService = pictureService;
    }

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData = null)
    {
        if (!string.Equals(ViewContext?.RouteData.Values["controller"]?.ToString(), "Home", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(ViewContext.RouteData.Values["action"]?.ToString(), "Index", StringComparison.OrdinalIgnoreCase))
            return new HtmlContentViewComponentResult(HtmlString.Empty);

        var store = await _storeContext.GetCurrentStoreAsync();
        var storeUrl = store.Url?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(storeUrl))
            return new HtmlContentViewComponentResult(HtmlString.Empty);

        var onlineStore = new Dictionary<string, object>
        {
            ["@type"] = "OnlineStore",
            ["@id"] = $"{storeUrl}#organization",
            ["name"] = "Hood Archery Shop",
            ["url"] = $"{storeUrl}/",
            ["contactPoint"] = new Dictionary<string, object>
            {
                ["@type"] = "ContactPoint",
                ["telephone"] = store.CompanyPhoneNumber,
                ["email"] = "info@hoodarcheryshop.com"
            },
            ["hasMerchantReturnPolicy"] = new Dictionary<string, object>
            {
                ["@type"] = "MerchantReturnPolicy",
                ["merchantReturnLink"] = $"{storeUrl}/en/shipping-returns"
            }
        };

        if (!string.IsNullOrWhiteSpace(store.CompanyName))
            onlineStore["legalName"] = store.CompanyName;

        if (!string.IsNullOrWhiteSpace(store.CompanyAddress))
        {
            onlineStore["address"] = new Dictionary<string, object>
            {
                ["@type"] = "PostalAddress",
                ["streetAddress"] = store.CompanyAddress
            };
        }

        if (_storeInformationSettings.LogoPictureId > 0)
        {
            var logoUrl = await _pictureService.GetPictureUrlAsync(_storeInformationSettings.LogoPictureId, showDefaultPicture: false);
            if (!string.IsNullOrWhiteSpace(logoUrl))
                onlineStore["logo"] = logoUrl;
        }

        var sameAs = new[]
            {
                _storeInformationSettings.FacebookLink,
                _storeInformationSettings.TwitterLink,
                _storeInformationSettings.YoutubeLink,
                _storeInformationSettings.InstagramLink
            }
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (sameAs.Count > 0)
            onlineStore["sameAs"] = sameAs;

        var graph = new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org",
            ["@graph"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["@type"] = "WebSite",
                    ["@id"] = $"{storeUrl}#website",
                    ["name"] = "Hood Archery Shop",
                    ["url"] = $"{storeUrl}/"
                },
                onlineStore
            }
        };

        var json = JsonSerializer.Serialize(graph, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        return new HtmlContentViewComponentResult(new HtmlString($"<script type=\"application/ld+json\">{json}</script>"));
    }
}
