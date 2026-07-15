using Microsoft.Extensions.DependencyInjection.Extensions;
using Postie.AspNetCore;
using Postie.AspNetCore.MediatR;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration for dispatching Postie endpoints through MediatR.
/// </summary>
public static class PostieMediatRServiceCollectionExtensions
{
    /// <summary>
    /// Registers the MediatR-backed endpoint dispatcher so <c>MapQuery</c>/<c>MapCommand</c> and friends
    /// dispatch through MediatR's <see cref="global::MediatR.ISender"/>.
    /// </summary>
    /// <remarks>
    /// Register MediatR itself separately (for example
    /// <c>services.AddMediatR(cfg =&gt; cfg.RegisterServicesFromAssembly(typeof(MyRequest).Assembly))</c>);
    /// this only wires the adapter that bridges Postie's endpoints to it.
    /// </remarks>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddPostieMediatR(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddTransient<IEndpointDispatcher, MediatREndpointDispatcher>();

        return services;
    }
}
