using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Nop.Services.Logging;

namespace Nop.Plugin.Widgets.CustomProductReviews.Services
{
    public class QueueService:BackgroundService
    {
        private IBackgroundQueue _queue;
        private readonly ILogger _logger;
        public QueueService(IBackgroundQueue queue, ILogger logger)
        {
            _queue = queue;
            _logger = logger;
        }
        protected async override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (stoppingToken.IsCancellationRequested==false)
            {
                var task = await _queue.PopQueue(stoppingToken);

                try
                {
                    await task(stoppingToken);
                }
                catch (Exception exception)
                {
                    // A malformed upload must be visible to administrators but may
                    // not terminate the worker and strand subsequent review media.
                    await _logger.ErrorAsync("Custom Product Reviews background media processing failed.", exception);
                }
            }
        }
    }
}
