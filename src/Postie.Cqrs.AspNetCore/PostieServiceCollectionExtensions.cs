using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Postie.AspNetCore;
using Postie.Cqrs.AspNetCore;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration for the Postie mediator together with the ASP.NET Core endpoint dispatcher.
/// </summary>
public static class PostieServiceCollectionExtensions
{
    /// <summary>
    /// Registers Postie's command and query handlers from the given assemblies and wires the endpoint
    /// dispatcher so <c>MapQuery</c>/<c>MapCommand</c> and friends dispatch through Postie's mediator.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="assemblies">The assemblies to scan for handlers. Defaults to the calling assembly when none are supplied.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddPostie(this IServiceCollection services, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (assemblies is null || assemblies.Length == 0)
        {
            assemblies = [Assembly.GetCallingAssembly()];
        }

        services.AddCqrs(assemblies);
        services.AddPostieEndpointDispatcher();

        return services;
    }

    /// <summary>
    /// Registers only the Postie endpoint dispatcher, for when the command and query handlers are
    /// registered separately.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddPostieEndpointDispatcher(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddTransient<IEndpointDispatcher, PostieEndpointDispatcher>();

        return services;
    }
}
