using Azure;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Quartz;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using JsonSerializer = System.Text.Json.JsonSerializer;
using Path = System.IO.Path;
using SHA256 = System.Security.Cryptography.SHA256;

using System.Net;
using Azure.Core;
using System.Xml.Serialization;

using System.Runtime.Serialization.Formatters.Binary;
using Nop.Plugin.Misc.EtsyToNopcommerce.Services;


namespace Nop.Plugin.Misc.EtsyToNopcommerce.Job
{
    [DisallowConcurrentExecution]
    public class JobGetReviews : IJob
    {
        private readonly ILogger _loglist;
        private readonly IEtsyApiService _etsyApiService;
        public JobGetReviews(ILogger logger, IEtsyApiService etsyApiService)
        {
            _loglist = logger;
            _etsyApiService = etsyApiService;
        }

        public string PrettyJson(string unPrettyJson)
        {
            var options = new JsonSerializerOptions()
            {
                WriteIndented = true,
                IncludeFields = true
            };

            var jsonElement = JsonSerializer.Deserialize<JsonElement>(unPrettyJson);

            return JsonSerializer.Serialize(jsonElement, options);
        }

        public virtual async Task Execute(IJobExecutionContext context)
        {
          var result=  await _etsyApiService.GetAllEtsyReviews();
          
          if (result.Any())
          {
              _loglist.LogDebug("JobGetReviewsResult: " + "/" + result + " at {DT}",
                  DateTime.UtcNow.ToLongTimeString());
              
              Console.WriteLine(
              "JobGetReviewsResult: " + "/" + result + " at " + DateTime.UtcNow.ToLongTimeString());
          }


          await Task.CompletedTask;
        }

       
    }
}
