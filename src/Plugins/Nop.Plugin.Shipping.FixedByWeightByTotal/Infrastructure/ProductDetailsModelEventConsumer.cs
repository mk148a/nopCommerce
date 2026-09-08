using Nop.Services.Events;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Services.ProductionTime;
using Nop.Web.Framework.Events;
using Nop.Web.Framework.Models;
using Nop.Web.Models.Catalog;

namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Infrastructure;

/// <summary>
/// Removes internal inventory and generic delivery labels from the server-rendered
/// product model when the typed production-time record identifies an orderable
/// handmade item. Inventory and checkout data remain unchanged in the domain model.
/// </summary>
public sealed class ProductDetailsModelEventConsumer : IConsumer<ModelPreparedEvent<BaseNopModel>>
{
    private readonly IProductProductionTimeService _productionTimeService;

    public ProductDetailsModelEventConsumer(IProductProductionTimeService productionTimeService)
    {
        _productionTimeService = productionTimeService;
    }

    public async Task HandleEventAsync(ModelPreparedEvent<BaseNopModel> eventMessage)
    {
        if (eventMessage?.Model is not ProductDetailsModel model || model.Id <= 0)
            return;

        var production = await _productionTimeService.GetModelByProductIdAsync(model.Id);
        if (production?.HideNumericStock != true)
            return;

        model.StockAvailability = null;
        model.DeliveryDate = null;
    }
}
