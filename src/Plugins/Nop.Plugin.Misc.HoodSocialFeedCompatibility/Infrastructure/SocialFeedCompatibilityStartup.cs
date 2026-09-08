using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;

namespace Nop.Plugin.Misc.HoodSocialFeedCompatibility.Infrastructure;

/// <summary>
/// The vendor plugin's dependency registrar is not discovered by the current
/// nopCommerce host. Register its own public service pairs at startup instead
/// of changing vendor binaries or nopCommerce core.
/// </summary>
public sealed class SocialFeedCompatibilityStartup : INopStartup
{
    private const string SocialFeedAssemblyName = "SevenSpikes.Nop.Plugins.SocialFeed";
    private const string ServicesNamespace = "SevenSpikes.Nop.Plugins.SocialFeed.Services.";
    private const string HelpersNamespace = "SevenSpikes.Nop.Plugins.SocialFeed.Helpers.";
    private const string ModelBuildersNamespace = "SevenSpikes.Nop.Plugins.SocialFeed.ModelBuilders.";
    private const string AdminNamespace = "SevenSpikes.Nop.Plugins.SocialFeed.Areas.Admin.";

    private static readonly (string Contract, string Implementation)[] ServicePairs =
    [
        (ServicesNamespace + "ISocialFeedService", ServicesNamespace + "SocialFeedService"),
        (ServicesNamespace + "ISocialNetworkService", ServicesNamespace + "SocialNetworkService"),
        (ServicesNamespace + "IFacebookService", ServicesNamespace + "FacebookService"),
        (ServicesNamespace + "IInstagramService", ServicesNamespace + "InstagramService"),
        (HelpersNamespace + "IWebConfigHelper", HelpersNamespace + "WebConfigHelper"),
        (ModelBuildersNamespace + "ISocialFeedModelBuilder", ModelBuildersNamespace + "SocialFeedModelBuilder"),
        (ModelBuildersNamespace + "ISocialMediaModelBuilder", ModelBuildersNamespace + "SocialMediaModelBuilder"),
        (ModelBuildersNamespace + "IFacebookModelBuilder", ModelBuildersNamespace + "FacebookModelBuilder"),
        (ModelBuildersNamespace + "IInstagramModelBuilder", ModelBuildersNamespace + "InstagramModelBuilder"),
        (ModelBuildersNamespace + "ITwitterModelBuilder", ModelBuildersNamespace + "TwitterModelBuilder"),
        (AdminNamespace + "Factories.ISocialFeedModelFactory", AdminNamespace + "Factories.SocialFeedModelFactory"),
        (AdminNamespace + "Helpers.ISocialNetworkEntitySettingsHelper", AdminNamespace + "Helpers.SocialNetworkEntitySettingsHelper")
    ];

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var socialFeedAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, SocialFeedAssemblyName, StringComparison.Ordinal));

        if (socialFeedAssembly is null)
        {
            try
            {
                socialFeedAssembly = Assembly.Load(new AssemblyName(SocialFeedAssemblyName));
            }
            catch (FileNotFoundException)
            {
                // The declared plugin dependency is not installed, so there is no
                // vendor service to bridge. Leave the host untouched.
                return;
            }
        }

        foreach (var (contractName, implementationName) in ServicePairs)
        {
            var contract = socialFeedAssembly.GetType(contractName, throwOnError: false);
            var implementation = socialFeedAssembly.GetType(implementationName, throwOnError: false);

            if (contract is not null && implementation is not null && contract.IsAssignableFrom(implementation))
                services.AddScoped(contract, implementation);
        }

        // This vendor package declares ITwitterService in its controller, but does
        // not ship an implementation. A no-op proxy makes the administration page
        // usable without inventing Twitter data or triggering an external request.
        var twitterService = socialFeedAssembly.GetType(ServicesNamespace + "ITwitterService", throwOnError: false);
        if (twitterService is not null && twitterService.IsInterface)
            services.AddScoped(twitterService, _ => CreateNoOpProxy(twitterService));
    }

    public void Configure(IApplicationBuilder application)
    {
    }

    // Register after vendor startup so the bridge is the effective registration.
    public int Order => 1000;

    private static object CreateNoOpProxy(Type contract)
    {
        var createMethod = typeof(DispatchProxy).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == nameof(DispatchProxy.Create) && method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 2);

        return createMethod.MakeGenericMethod(contract, typeof(NoOpTwitterServiceProxy)).Invoke(null, null)!;
    }

    public class NoOpTwitterServiceProxy : DispatchProxy
    {
        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            var returnType = targetMethod?.ReturnType ?? typeof(void);

            if (returnType == typeof(void))
                return null;

            if (returnType == typeof(Task))
                return Task.CompletedTask;

            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var resultType = returnType.GenericTypeArguments[0];
                var result = resultType.IsValueType ? Activator.CreateInstance(resultType) : null;
                return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(resultType).Invoke(null, [result]);
            }

            if (returnType == typeof(ValueTask) || (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>)))
                return Activator.CreateInstance(returnType);

            return returnType.IsValueType ? Activator.CreateInstance(returnType) : null;
        }
    }
}
