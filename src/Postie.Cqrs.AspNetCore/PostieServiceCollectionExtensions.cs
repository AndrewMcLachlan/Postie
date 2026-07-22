using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Postie.AspNetCore;
using Postie.Cqrs.AspNetCore;
using Postie.Cqrs.Queries;

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
    /// <param name="assemblies">The assemblies to scan for handlers. At least one is required.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddPostie(this IServiceCollection services, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        if (assemblies.Length == 0)
        {
            throw new ArgumentException("Specify at least one assembly to scan for handlers, or use AddPostie<TMarker>().", nameof(assemblies));
        }

        services.AddCqrs(assemblies);
        services.AddPostieEndpointDispatcher();

        return services;
    }

    /// <summary>
    /// Registers Postie's command and query handlers from the assembly containing
    /// <typeparamref name="TMarker"/> and wires the endpoint dispatcher so
    /// <c>MapQuery</c>/<c>MapCommand</c> and friends dispatch through Postie's mediator.
    /// </summary>
    /// <typeparam name="TMarker">Any type from the assembly to scan for handlers.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddPostie<TMarker>(this IServiceCollection services) =>
        services.AddPostie(typeof(TMarker).Assembly);

    /// <summary>
    /// Registers only the Postie endpoint dispatcher, for when the command and query handlers are
    /// registered separately.
    /// </summary>
    /// <remarks>
    /// <see cref="IEndpointDispatcher"/> only needs <c>IQueryDispatcher</c> and <c>ICommandDispatcher</c>
    /// (from <c>AddQueryHandlers</c>/<c>AddCommandHandlers</c>). Streaming dispatch additionally requires
    /// <c>IStreamQueryDispatcher</c>, registered by <c>AddStreamQueryHandlers</c> (or <c>AddCqrs</c>) — only
    /// needed if the app maps streaming endpoints with <c>MapStreamQuery</c>.
    /// </remarks>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddPostieEndpointDispatcher(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddTransient<IEndpointDispatcher, PostieEndpointDispatcher>();

        // A factory keeps build-time DI validation from demanding stream query support in apps
        // that never map a stream endpoint; the dependency is checked on first use instead.
        services.TryAddTransient<IStreamEndpointDispatcher>(static provider =>
            new PostieStreamEndpointDispatcher(
                provider.GetService<IStreamQueryDispatcher>()
                    ?? throw new InvalidOperationException(
                        "Stream endpoint dispatch requires an IStreamQueryDispatcher, which is not registered. Register stream query support with AddCqrs or AddStreamQueryHandlers alongside AddPostieEndpointDispatcher.")));

        return services;
    }
}
