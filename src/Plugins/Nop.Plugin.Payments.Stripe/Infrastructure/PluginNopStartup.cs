using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Nop.Core.Infrastructure;
using Nop.Plugin.Payments.Stripe.Services;
using Nop.Services.Cms;
using Nop.Services.ScheduleTasks;

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
            services.AddScoped<IScheduleTask, StripePendingPaymentTask>();
            services.AddHttpClient<StripePaymentProcessor>();

            services.AddSingleton<IWidgetPlugin, StripeWidgetPlugin>();
            services.AddScoped<IPaymentStatusService, PaymentStatusService>();
        }

        public void Configure(IApplicationBuilder application)
        {
            application.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(
                    Path.Combine(Directory.GetCurrentDirectory(), "Plugins/Nop.Plugin.Payments.Stripe/Content")),
                RequestPath = "/Plugins/Payments.Stripe/Content"
            });
        }

        public int Order => 11;
    }
}