using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Nop.Plugin.Widgets.CustomProductReviews.Services
{
    public class QueueService : BackgroundService
    {
        private IBackgroundQueue _queue;
        public QueueService(IBackgroundQueue queue)
        {
            _queue = queue;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested) // İptal kontrolü ekle
            {
                try
                {
                    var task = await _queue.PopQueue(stoppingToken);
                    await task(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // İptal edildiğinde sessizce çık
                    break;
                }
                catch (Exception ex)
                {
                    // Loglama yap
                    Console.WriteLine($"Error processing task: {ex}");
                }
            }
        }
    }
}