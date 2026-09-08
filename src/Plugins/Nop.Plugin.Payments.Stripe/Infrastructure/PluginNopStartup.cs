using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Payments.Stripe.Services;
using Nop.Services.ScheduleTasks;
using Nop.Web.Areas.Admin.Factories;

namespace Nop.Plugin.Payments.Stripe.Infrastructure
{
    public class PluginNopStartup : INopStartup
    {
        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<RazorViewEngineOptions>(options =>
            {
                options.ViewLocationExpanders.Add(new ViewLocationExpander());
            });

            //register services and interfaces
            services.AddScoped<IPaymentStripeService, PaymentStripeService>();
            services.AddScoped<IStripeWebhookService, StripeWebhookService>();
            services.AddScoped<IScheduleTask, StripePendingPaymentTask>();
            services.AddHttpClient<StripePaymentProcessor>();
            services.AddScoped<IOrderModelFactory, OrderModelFactory>();
            services.AddScoped<StripePaymentProcessor>(); // Bu satırı ekleyin
        }

        public void Configure(IApplicationBuilder application)
        {
        }

        public int Order => 11;
    }
}
