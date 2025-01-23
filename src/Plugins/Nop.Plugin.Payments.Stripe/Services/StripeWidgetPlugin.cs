using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nop.Plugin.Payments.Stripe.Components;
using Nop.Services.Cms;
using Nop.Services.Plugins;

namespace Nop.Plugin.Payments.Stripe.Services;
public class StripeWidgetPlugin : BasePlugin, IWidgetPlugin
{
    public bool HideInWidgetList => true;

    public Type GetWidgetViewComponent(string widgetZone)
    {
        return widgetZone switch
        {
            "opc_content_after" => typeof(StripeScriptsViewComponent),
            "opc_errors_before" => typeof(StripeErrorsViewComponent),
            _ => null
        };
    }

    public Task<IList<string>> GetWidgetZonesAsync()
    {
        return Task.FromResult<IList<string>>(new List<string>
        {
            "opc_content_after",
            "opc_errors_before"
        });
    }
}
