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
    /// <param name="assemblies">The assemblies to scan for <see cref="IValidator{T}"/> implementations. Defaults to the calling assembly when none are supplied.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddPostieValidation(this IServiceCollection services, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (assemblies is null || assemblies.Length == 0)
        {
            assemblies = [Assembly.GetCallingAssembly()];
        }

        services.AddValidatorsFromAssemblies(assemblies, includeInternalTypes: true);

        services.AddQueryPipelineBehavior(typeof(QueryValidationBehavior<,>));
        services.AddCommandPipelineBehavior(typeof(CommandValidationBehavior<,>));
        services.AddCommandPipelineBehavior(typeof(CommandValidationBehavior<>));

        return services;
    }
}
