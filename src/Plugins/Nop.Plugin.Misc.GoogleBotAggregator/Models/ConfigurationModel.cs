using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Misc.GoogleBotAggregator.Models;

public record ConfigurationModel : BaseNopModel
{
    [NopResourceDisplayName("Plugins.Misc.GoogleBotAggregator.ExcludeFromAnalytics")]
    public bool ExcludeFromAnalytics { get; set; }

    [NopResourceDisplayName("Plugins.Misc.GoogleBotAggregator.MergeShoppingCarts")]
    public bool MergeShoppingCarts { get; set; }

    [NopResourceDisplayName("Plugins.Misc.GoogleBotAggregator.GoogleBotCustomerEmail")]
    public string GoogleBotCustomerEmail { get; set; }
} 