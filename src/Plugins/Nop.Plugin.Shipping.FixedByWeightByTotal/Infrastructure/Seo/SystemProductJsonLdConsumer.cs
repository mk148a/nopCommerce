using Nop.Services.Events;
using Nop.Web.Models.JsonLD;

namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Infrastructure.Seo;

/// <summary>
/// Suppresses Product JSON-LD for configured non-merchandise payment products without
/// changing their catalog route, cart behavior, price, or inventory.
/// </summary>
public sealed class SystemProductJsonLdConsumer : IConsumer<JsonLdCreatedEvent<JsonLdProductModel>>
{
    private readonly FixedByWeightByTotalSettings _settings;

    public SystemProductJsonLdConsumer(FixedByWeightByTotalSettings settings)
    {
        _settings = settings;
    }

    public Task HandleEventAsync(JsonLdCreatedEvent<JsonLdProductModel> eventMessage)
    {
        var sku = eventMessage?.Model?.Sku;
        if (string.IsNullOrWhiteSpace(sku))
            return Task.CompletedTask;

        var systemSkus = (_settings.SystemProductSkus ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (systemSkus.Contains(sku, StringComparer.OrdinalIgnoreCase))
        {
            eventMessage.Model.SuppressOutput = true;
            eventMessage.Model.SuppressIndex = true;
        }

        return Task.CompletedTask;
    }
}
