using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nop.Plugin.Misc.EtsyToNopcommerce.Services;
using Quartz;

namespace Nop.Plugin.Misc.EtsyToNopcommerce.Job
{
    [DisallowConcurrentExecution]
    public class JobGetListings : IJob
    {
        private readonly ILogger _loglist;
        private readonly IEtsyApiService _etsyApiService;

        public JobGetListings(ILogger logger, IEtsyApiService etsyApiService)
        {
            _loglist = logger;
            _etsyApiService = etsyApiService;
        }

        public virtual async Task Execute(IJobExecutionContext context)
        {
            var result = await _etsyApiService.ListingleriAlVeIsle();

            if (result.Any())
            {
                _loglist.LogDebug("JobGetEtsyListingsResult: " + "/" + result + " at {DT}",
                    DateTime.UtcNow.ToLongTimeString());

                Console.WriteLine(
                    "JobGetEtsyListingsResult: " + "/" + result + " at " + DateTime.UtcNow.ToLongTimeString());
            }

            await Task.CompletedTask;
        }
    }
}