using System.Reflection;
using FluentValidation;
using Postie.Cqrs.FluentValidation;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration for FluentValidation pipeline behaviors.
/// </summary>
public static class PostieValidationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the FluentValidation pipeline behaviors and the validators found in the given assemblies.
    /// Queries and commands are validated before their handler runs; a failure throws a
    /// <see cref="global::FluentValidation.ValidationException"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="assemblies">The assemblies to scan for <see cref="IValidator{T}"/> implementations. At least one is required.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddPostieValidation(this IServiceCollection services, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        // A calling-assembly default would usually pick the host assembly, not the one holding the
        // validators - and Assembly.GetCallingAssembly is unreliable under inlining anyway.
        if (assemblies.Length == 0)
        {
            throw new ArgumentException("Specify at least one assembly to scan for validators, or use AddPostieValidation<TMarker>().", nameof(assemblies));
        }

        services.AddValidatorsFromAssemblies(assemblies, includeInternalTypes: true);

        services.AddQueryPipelineBehavior(typeof(QueryValidationBehavior<,>));
        services.AddCommandPipelineBehavior(typeof(CommandValidationBehavior<,>));
        services.AddCommandPipelineBehavior(typeof(CommandValidationBehavior<>));

        return services;
    }

    /// <summary>
    /// Registers the FluentValidation pipeline behaviors and the validators found in the assembly
    /// containing <typeparamref name="TMarker"/>.
    /// </summary>
    /// <typeparam name="TMarker">Any type from the assembly to scan for validators.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddPostieValidation<TMarker>(this IServiceCollection services) =>
        services.AddPostieValidation(typeof(TMarker).Assembly);
}
