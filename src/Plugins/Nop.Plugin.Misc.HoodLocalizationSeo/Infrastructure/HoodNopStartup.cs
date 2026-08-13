using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Misc.HoodLocalizationSeo.Factories;
using Nop.Plugin.Misc.HoodLocalizationSeo.Services;
using Nop.Web.Factories;
using Nop.Web.Framework.Mvc.Routing;
using PublicWidgetModelFactory = Nop.Web.Framework.Factories.IWidgetModelFactory;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Infrastructure;

public sealed class HoodNopStartup : INopStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IBlogLocalizationService, BlogLocalizationService>();
        services.AddScoped<IBlogRouteLanguageResolver, BlogRouteLanguageResolver>();
        services.AddScoped<IBlogTagHreflangService, BlogTagHreflangService>();
        services.AddScoped<ILocalizationResourceInstaller, LocalizationResourceInstaller>();
        services.AddScoped<ISitemapArtifactInvalidator, SitemapArtifactInvalidator>();
        services.AddScoped<LocalizedBlogRedirectFilter>();

        Decorate<IBlogModelFactory, LocalizedBlogModelFactory>(services);
        Decorate<INopUrlHelper, LocalizedBlogUrlHelper>(services);
        Decorate<ISitemapModelFactory, LocalizedSitemapModelFactory>(services);
        Decorate<PublicWidgetModelFactory, BlogTagHreflangWidgetModelFactory>(services);

        services.Configure<MvcOptions>(options =>
            options.Filters.AddService<LocalizedBlogRedirectFilter>());
    }

    public void Configure(IApplicationBuilder application)
    {
    }

    public int Order => 3000;

    internal static void Decorate<TService, TDecorator>(IServiceCollection services)
        where TService : class
        where TDecorator : class, TService
    {
        var descriptor = services.LastOrDefault(item => item.ServiceType == typeof(TService) &&
                                                  !item.IsKeyedService)
            ?? throw new InvalidOperationException($"{typeof(TService).FullName} is not registered.");

        services.Remove(descriptor);
        services.Add(ServiceDescriptor.Describe(typeof(DecorationInner<TService, TDecorator>), provider =>
            CreateInner<TService, TDecorator>(provider, descriptor), descriptor.Lifetime));
        services.Add(ServiceDescriptor.Describe(typeof(TService), provider =>
            ActivatorUtilities.CreateInstance<TDecorator>(provider,
                provider.GetRequiredService<DecorationInner<TService, TDecorator>>().Value), descriptor.Lifetime));
    }

    private static DecorationInner<TService, TDecorator> CreateInner<TService, TDecorator>(
        IServiceProvider provider, ServiceDescriptor descriptor)
        where TService : class
        where TDecorator : class, TService
    {
        var ownsInstance = descriptor.ImplementationInstance is null;
        var implementation = descriptor.ImplementationInstance ??
                             descriptor.ImplementationFactory?.Invoke(provider) ??
                             (descriptor.ImplementationType is not null
                                 ? ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType)
                                 : throw new InvalidOperationException(
                                     $"Cannot materialize registration for {typeof(TService).FullName}."));
        if (implementation is not TService service)
            throw new InvalidOperationException(
                $"Registration for {typeof(TService).FullName} produced {implementation?.GetType().FullName ?? "null"}.");

        return new DecorationInner<TService, TDecorator>(service, ownsInstance);
    }

    private sealed class DecorationInner<TService, TDecorator> : IDisposable, IAsyncDisposable
        where TService : class
        where TDecorator : class, TService
    {
        private readonly bool _ownsInstance;
        private bool _disposed;

        public DecorationInner(TService value, bool ownsInstance)
        {
            Value = value;
            _ownsInstance = ownsInstance;
        }

        public TService Value { get; }

        public void Dispose()
        {
            if (_disposed || !_ownsInstance)
                return;

            _disposed = true;
            if (Value is IDisposable disposable)
                disposable.Dispose();
            else if (Value is IAsyncDisposable asyncDisposable)
                asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed || !_ownsInstance)
                return;

            _disposed = true;
            if (Value is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
            else if (Value is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
