using Microsoft.Extensions.DependencyInjection;
using Nop.Plugin.Misc.HoodLocalizationSeo.Infrastructure;
using NUnit.Framework;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Tests;

[TestFixture]
public sealed class HoodNopStartupTests
{
    [TestCase(ServiceLifetime.Scoped)]
    [TestCase(ServiceLifetime.Singleton)]
    [TestCase(ServiceLifetime.Transient)]
    public void DecoratesTypeRegistrationAndPreservesLifetime(ServiceLifetime lifetime)
    {
        var services = new ServiceCollection();
        ((IServiceCollection)services).Add(
            ServiceDescriptor.Describe(typeof(IProbe), typeof(DisposableProbe), lifetime));

        HoodNopStartup.Decorate<IProbe, ProbeDecorator>(services);

        Assert.That(services.Last(descriptor => descriptor.ServiceType == typeof(IProbe)).Lifetime,
            Is.EqualTo(lifetime));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var decorated = scope.ServiceProvider.GetRequiredService<IProbe>();
        Assert.That(decorated, Is.TypeOf<ProbeDecorator>());
        Assert.That(((ProbeDecorator)decorated).Inner, Is.TypeOf<DisposableProbe>());
    }

    [Test]
    public void DecoratesFactoryRegistrationAndDisposesFactoryProduct()
    {
        var services = new ServiceCollection();
        var inner = new DisposableProbe();
        services.AddScoped<IProbe>(_ => inner);
        HoodNopStartup.Decorate<IProbe, ProbeDecorator>(services);

        using (var provider = services.BuildServiceProvider())
        using (var scope = provider.CreateScope())
            Assert.That(scope.ServiceProvider.GetRequiredService<IProbe>(), Is.TypeOf<ProbeDecorator>());

        Assert.That(inner.Disposed, Is.True);
    }

    [Test]
    public void DecoratesInstanceRegistrationWithoutOwningInstance()
    {
        var services = new ServiceCollection();
        var inner = new DisposableProbe();
        services.AddSingleton<IProbe>(inner);
        HoodNopStartup.Decorate<IProbe, ProbeDecorator>(services);

        using (var provider = services.BuildServiceProvider())
            Assert.That(provider.GetRequiredService<IProbe>(), Is.TypeOf<ProbeDecorator>());

        Assert.That(inner.Disposed, Is.False);
        inner.Dispose();
    }

    [Test]
    public void IndependentDecoratorsCanBeComposed()
    {
        var services = new ServiceCollection();
        services.AddScoped<IProbe, DisposableProbe>();
        HoodNopStartup.Decorate<IProbe, ProbeDecorator>(services);
        HoodNopStartup.Decorate<IProbe, OuterProbeDecorator>(services);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var outer = (OuterProbeDecorator)scope.ServiceProvider.GetRequiredService<IProbe>();
        var inner = (ProbeDecorator)outer.Inner;
        Assert.That(inner.Inner, Is.TypeOf<DisposableProbe>());
    }

    private interface IProbe;

    private sealed class DisposableProbe : IProbe, IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    private sealed class ProbeDecorator : IProbe
    {
        public ProbeDecorator(IProbe inner) => Inner = inner;
        public IProbe Inner { get; }
    }

    private sealed class OuterProbeDecorator : IProbe
    {
        public OuterProbeDecorator(IProbe inner) => Inner = inner;
        public IProbe Inner { get; }
    }
}
