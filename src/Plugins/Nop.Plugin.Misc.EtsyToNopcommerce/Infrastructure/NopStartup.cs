using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nop.Core.Infrastructure;
using Nop.Plugin.Misc.EtsyToNopcommerce.Job;
using Nop.Plugin.Misc.EtsyToNopcommerce.Services;
using Nop.Services.Plugins;
using Quartz;
using Quartz.AspNetCore;

namespace Nop.Plugin.Misc.EtsyToNopcommerce.Infrastructure
{
    public class NopStartup : INopStartup
    {
        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<RazorViewEngineOptions>(options =>
            {
                options.ViewLocationExpanders.Add(new ViewLocationExpander());
            });

            //register services and interfaces
            services.AddScoped<IProductReviewsEtsyReviewService, ProductReviewsEtsyReviewService>();
            services.AddScoped<IEtsyListingsService, EtsyListingsService>();
            services.AddScoped<IEtsyCustomersService, EtsyCustomersService>();
            services.AddScoped<ICustomProductReviewMappingService, CustomProductReviewMappingService>();
            services.AddScoped<IProductReviewsTransactionsMappingService, ProductReviewsTransactionsMappingService>();
            services.AddScoped<IEtsyApiService, EtsyApiService>();

            services.AddSingleton<IBackgroundQueue, BackgroundQueue>();
            services.AddHostedService<QueueService>();

            //Quartz Server codes

            services.AddLogging();
            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetService<ILogger<JobGetReviews>>();
           
            services.AddSingleton(typeof(ILogger), logger);

            var logger1 = serviceProvider.GetService<ILogger<JobGetListings>>();
            services.AddSingleton(typeof(ILogger), logger1);

            services.AddQuartz(q =>
            {
                q.UseMicrosoftDependencyInjectionJobFactory();

                q.ScheduleJob<JobGetReviews>(TJobGetReviews => TJobGetReviews
                    .WithIdentity("TJobGetReviews")
                    .StartNow()
                    .WithDailyTimeIntervalSchedule(x => x.WithInterval(2, IntervalUnit.Minute))
                    .WithDescription("TJobGetReviews")
                );


            });

            services.AddQuartz(q =>
            {
                q.UseMicrosoftDependencyInjectionJobFactory();

                q.ScheduleJob<JobGetListings>(TJobGetListings => TJobGetListings
                    .WithIdentity("TJobGetListings")
                    .StartNow()
                    .WithDailyTimeIntervalSchedule(x => x.WithInterval(5, IntervalUnit.Minute))
                    .WithDescription("TJobGetListings")
                );


            });

            services.AddQuartz(q =>
            {
                q.UseMicrosoftDependencyInjectionJobFactory();

                q.ScheduleJob<JobGetCustomers>(TJobGetCustomers => TJobGetCustomers
                    .WithIdentity("TJobGetCustomers")
                    .StartNow()
                    .WithDailyTimeIntervalSchedule(x => x.WithInterval(5, IntervalUnit.Minute))
                    .WithDescription("TJobGetCustomers")
                );


            });

            //ASP.NET Core hosting
            services.AddQuartzServer(options =>
            {
                // when shutting down we want jobs to complete gracefully
                options.WaitForJobsToComplete = true;
            });

        }

        public void Configure(IApplicationBuilder application)
        {
            
        }

        public int Order => 11;
    }
}