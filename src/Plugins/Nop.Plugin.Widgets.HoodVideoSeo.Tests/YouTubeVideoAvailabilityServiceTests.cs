using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Nop.Plugin.Widgets.HoodVideoSeo.Infrastructure;
using Nop.Plugin.Widgets.HoodVideoSeo.Services;
using NUnit.Framework;

namespace Nop.Plugin.Widgets.HoodVideoSeo.Tests;

[TestFixture]
public sealed class YouTubeVideoAvailabilityServiceTests
{
    [Test]
    public async Task CacheMissReturnsImmediatelyAndRefreshesInBackground()
    {
        using var handler = new ControlledHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.youtube.com/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(client, cache);

        var stopwatch = Stopwatch.StartNew();
        var result = service.GetAvailability("0KEeQy_QZeg");
        stopwatch.Stop();

        Assert.That(result, Is.Null);
        Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromMilliseconds(250)));
        await WaitUntilAsync(() => handler.CallCount == 1);

        handler.CompleteValidOEmbed("0KEeQy_QZeg");
        await WaitUntilAsync(() => service.GetAvailability("0KEeQy_QZeg") == true);
        Assert.That(handler.CallCount, Is.EqualTo(1));
    }

    [Test]
    public async Task ConcurrentCacheMissesQueueOnlyOneRefresh()
    {
        using var handler = new ControlledHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.youtube.com/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(client, cache);

        var results = Enumerable.Range(0, 50)
            .Select(_ => service.GetAvailability("_YKlGXkylzU"))
            .ToArray();

        Assert.That(results, Is.All.Null);
        await WaitUntilAsync(() => handler.CallCount == 1);
        Assert.That(handler.CallCount, Is.EqualTo(1));

        handler.CompleteValidOEmbed("_YKlGXkylzU");
        await WaitUntilAsync(() => service.GetAvailability("_YKlGXkylzU") == true);
    }

    [Test]
    public async Task ColdCacheOnlyReadsForManyIdsDoNotQueueNetworkRequests()
    {
        using var handler = new ControlledHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.youtube.com/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(client, cache);

        var results = Enumerable.Range(0, 100)
            .Select(index => service.GetCachedAvailability($"v{index:0000000000}"))
            .ToArray();

        await Task.Delay(100);
        Assert.That(results, Is.All.Null);
        Assert.That(handler.CallCount, Is.Zero);
    }

    [Test]
    public async Task InconclusiveResponseIsCachedAsUnknownCooldown()
    {
        using var handler = new ControlledHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.youtube.com/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(client, cache);

        Assert.That(service.GetAvailability("1SKZEPYjdoo"), Is.Null);
        await WaitUntilAsync(() => handler.CallCount == 1);
        handler.Complete(HttpStatusCode.TooManyRequests);
        await WaitUntilAsync(() => cache.TryGetValue("hood.video-seo.availability.1SKZEPYjdoo", out _));

        Assert.That(service.GetAvailability("1SKZEPYjdoo"), Is.Null);
        Assert.That(handler.CallCount, Is.EqualTo(1));
    }

    [Test]
    public async Task NotFoundIsCachedAsUnavailable()
    {
        using var handler = new ControlledHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.youtube.com/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(client, cache);

        Assert.That(service.GetAvailability("jpQ-VZPb_Pk"), Is.Null);
        await WaitUntilAsync(() => handler.CallCount == 1);
        handler.Complete(HttpStatusCode.NotFound);
        await WaitUntilAsync(() => service.GetCachedAvailability("jpQ-VZPb_Pk") == false);

        Assert.That(service.GetAvailability("jpQ-VZPb_Pk"), Is.False);
        Assert.That(handler.CallCount, Is.EqualTo(1));
    }

    [Test]
    public async Task OkWithoutJsonOEmbedProofRemainsUnknown()
    {
        using var handler = new ControlledHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.youtube.com/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(client, cache);

        Assert.That(service.GetAvailability("Qp0BAK-6ER4"), Is.Null);
        await WaitUntilAsync(() => handler.CallCount == 1);
        handler.Complete(HttpStatusCode.OK, "text/html", "<html>not oembed</html>");
        await WaitUntilAsync(() => cache.TryGetValue("hood.video-seo.availability.Qp0BAK-6ER4", out _));

        Assert.That(service.GetCachedAvailability("Qp0BAK-6ER4"), Is.Null);
        Assert.That(handler.CallCount, Is.EqualTo(1));
    }

    [Test]
    public async Task ColdRefreshFanOutIsBounded()
    {
        using var handler = new ControlledHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.youtube.com/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(client, cache);

        for (var index = 0; index < 100; index++)
            Assert.That(service.GetAvailability($"v{index:0000000000}"), Is.Null);

        await WaitUntilAsync(() => handler.CallCount == YouTubeVideoAvailabilityService.MaxConcurrentRefreshes);
        Assert.That(handler.CallCount, Is.EqualTo(YouTubeVideoAvailabilityService.MaxConcurrentRefreshes));

        handler.Complete(HttpStatusCode.TooManyRequests);
        await Task.Delay(250);
        Assert.That(handler.CallCount, Is.LessThanOrEqualTo(YouTubeVideoAvailabilityService.MaxPendingRefreshes));
    }

    [Test]
    public void InvalidIdFailsClosedWithoutNetworkCall()
    {
        using var handler = new ControlledHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.youtube.com/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(client, cache);

        Assert.That(service.GetAvailability("not-valid"), Is.False);
        Assert.That(handler.CallCount, Is.Zero);
    }

    [Test]
    public void NamedClientRegistrationHasShortBoundedTimeout()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        new PluginNopStartup().ConfigureServices(services, new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        using var client = provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(YouTubeVideoAvailabilityService.HttpClientName);

        Assert.That(client.Timeout, Is.EqualTo(TimeSpan.FromSeconds(3)));
        Assert.That(client.BaseAddress, Is.EqualTo(new Uri("https://www.youtube.com/")));
        Assert.That(client.DefaultRequestHeaders.UserAgent.ToString(), Does.Contain("HoodArcheryShopVideoSeo/1.0"));
    }

    private static YouTubeVideoAvailabilityService CreateService(HttpClient client, IMemoryCache cache)
    {
        return new YouTubeVideoAvailabilityService(
            new StubHttpClientFactory(client),
            NullLogger<YouTubeVideoAvailabilityService>.Instance,
            cache);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        Assert.That(condition(), Is.True);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StubHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name)
        {
            Assert.That(name, Is.EqualTo(YouTubeVideoAvailabilityService.HttpClientName));
            return _client;
        }
    }

    private sealed class ControlledHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource<HttpResponseMessage> _response =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public void Complete(HttpStatusCode statusCode)
        {
            Complete(statusCode, null, null);
        }

        public void CompleteValidOEmbed(string youtubeId)
        {
            var json = $$"""{"type":"video","provider_name":"YouTube","html":"<iframe src=\"https://www.youtube.com/embed/{{youtubeId}}\"></iframe>"}""";
            Complete(HttpStatusCode.OK, "application/json", json);
        }

        public void Complete(HttpStatusCode statusCode, string mediaType, string body)
        {
            var response = new HttpResponseMessage(statusCode);
            if (body is not null)
                response.Content = new StringContent(body, System.Text.Encoding.UTF8, mediaType);
            _response.TrySetResult(response);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            return _response.Task;
        }
    }
}
