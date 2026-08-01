using Nop.Core.Domain.Orders;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Domain;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Models.ProductionTime;

namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Services.ProductionTime;

public interface IProductProductionTimeService
{
    Task<HoodProductProductionTime> GetByProductIdAsync(int productId, bool activeOnly = false);
    Task<ProductProductionTimeModel> GetModelByProductIdAsync(int productId);
    Task SaveAsync(ProductProductionTimeModel model, string source = "Admin manual");
    Task<int> GetMaxProductionDaysForCartItemsAsync(IEnumerable<ShoppingCartItem> cartItems);
    Task<(int MinDays, int MaxDays)> GetProductionDayRangeForCartItemsAsync(IEnumerable<ShoppingCartItem> cartItems);
    Task<IList<ProductProductionTimeModel>> SearchAsync(int productId = 0, bool onlyEnabled = false, int pageSize = 200);
}
