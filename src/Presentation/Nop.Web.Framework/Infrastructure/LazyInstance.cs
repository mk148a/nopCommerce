using System;
using Microsoft.Extensions.DependencyInjection;

namespace Nop.Web.Framework.Infrastructure
{
    /// <summary>
    /// Represents a class calls the service provider’s method to get instance from container
    /// </summary>
    public class LazyInstance<T> : Lazy<T>
    {
        public LazyInstance(IServiceProvider serviceProvider)
            : base(() => serviceProvider.GetRequiredService<T>())
        {
        }
    }
}
