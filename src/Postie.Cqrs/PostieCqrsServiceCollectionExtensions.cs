using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Postie.Cqrs;
using Postie.Cqrs.Commands;
using Postie.Cqrs.Queries;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extensions for registering Postie CQRS services on an <see cref="IServiceCollection"/>.
/// </summary>
public static class PostieCqrsServiceCollectionExtensions
{
    private static readonly Type CommandHandlerGenericType = typeof(ICommandHandler<,>);
    private static readonly Type CommandHandlerVoidGenericType = typeof(ICommandHandler<>);
    private static readonly Type QueryHandlerGenericType = typeof(IQueryHandler<,>);

    /// <summary>
    /// Adds the command and query dispatchers and registers every command and query handler found in
    /// the given assemblies.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="assemblies">The assemblies to scan for handlers. Defaults to the calling assembly when none are supplied.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddCqrs(this IServiceCollection services, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (assemblies is null || assemblies.Length == 0)
        {
            assemblies = [Assembly.GetCallingAssembly()];
        }

        foreach (var assembly in assemblies)
        {
            services.AddCommandHandlers(assembly);
            services.AddQueryHandlers(assembly);
        }

        return services;
    }

    #region Commands
    /// <summary>
    /// Adds the command dispatcher and registers command handlers found in the given assembly.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="commandsAssembly">The assembly containing the command handlers.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddCommandHandlers(this IServiceCollection services, Assembly commandsAssembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(commandsAssembly);

        foreach (var type in commandsAssembly.DefinedTypes)
        {
            if (!type.IsClass || type.IsAbstract || type.IsGenericTypeDefinition)
                continue;

            var commandHandlerInterfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType &&
                           (i.GetGenericTypeDefinition() == CommandHandlerGenericType ||
                            i.GetGenericTypeDefinition() == CommandHandlerVoidGenericType));

            foreach (var interfaceType in commandHandlerInterfaces)
            {
                GuardAgainstDuplicateHandler(services, interfaceType, type);
                services.TryAddEnumerable(ServiceDescriptor.Transient(interfaceType, type));
            }
        }

        services.TryAddTransient<ICommandDispatcher, Dispatcher>();

        return services;
    }

    /// <summary>
    /// Adds an individual command handler for a command that returns a response.
    /// </summary>
    /// <typeparam name="THandler">The type of the handler.</typeparam>
    /// <typeparam name="TRequest">The type of the command.</typeparam>
    /// <typeparam name="TResponse">The type of the command response.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddCommandHandler<THandler, TRequest, TResponse>(this IServiceCollection services)
        where THandler : class, ICommandHandler<TRequest, TResponse>
        where TRequest : ICommand<TResponse>
    {
        ArgumentNullException.ThrowIfNull(services);

        GuardAgainstDuplicateHandler(services, typeof(ICommandHandler<TRequest, TResponse>), typeof(THandler));
        services.TryAddEnumerable(ServiceDescriptor.Transient<ICommandHandler<TRequest, TResponse>, THandler>());
        services.TryAddTransient<ICommandDispatcher, Dispatcher>();

        return services;
    }

    /// <summary>
    /// Adds an individual command handler for a command that does not return a response.
    /// </summary>
    /// <typeparam name="THandler">The type of the handler.</typeparam>
    /// <typeparam name="TRequest">The type of the command.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddCommandHandler<THandler, TRequest>(this IServiceCollection services)
        where THandler : class, ICommandHandler<TRequest>
        where TRequest : ICommand
    {
        ArgumentNullException.ThrowIfNull(services);

        GuardAgainstDuplicateHandler(services, typeof(ICommandHandler<TRequest>), typeof(THandler));
        services.TryAddEnumerable(ServiceDescriptor.Transient<ICommandHandler<TRequest>, THandler>());
        services.TryAddTransient<ICommandDispatcher, Dispatcher>();

        return services;
    }
    #endregion

    #region Queries
    /// <summary>
    /// Adds the query dispatcher and registers query handlers found in the given assembly.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="assembly">The assembly containing the query handlers.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddQueryHandlers(this IServiceCollection services, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        foreach (var type in assembly.DefinedTypes)
        {
            if (!type.IsClass || type.IsAbstract || type.IsGenericTypeDefinition) continue;

            var queryHandlerInterfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == QueryHandlerGenericType);

            foreach (var interfaceType in queryHandlerInterfaces)
            {
                GuardAgainstDuplicateHandler(services, interfaceType, type);
                services.TryAddEnumerable(ServiceDescriptor.Transient(interfaceType, type));
            }
        }

        services.TryAddTransient<IQueryDispatcher, Dispatcher>();

        return services;
    }

    /// <summary>
    /// Adds an individual query handler.
    /// </summary>
    /// <typeparam name="THandler">The type of the handler.</typeparam>
    /// <typeparam name="TRequest">The type of the query.</typeparam>
    /// <typeparam name="TResponse">The type of the query response.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddQueryHandler<THandler, TRequest, TResponse>(this IServiceCollection services)
        where THandler : class, IQueryHandler<TRequest, TResponse>
        where TRequest : IQuery<TResponse>
    {
        ArgumentNullException.ThrowIfNull(services);

        GuardAgainstDuplicateHandler(services, typeof(IQueryHandler<TRequest, TResponse>), typeof(THandler));
        services.TryAddEnumerable(ServiceDescriptor.Transient<IQueryHandler<TRequest, TResponse>, THandler>());
        services.TryAddTransient<IQueryDispatcher, Dispatcher>();

        return services;
    }
    #endregion

    // A request may have only one handler: the dispatcher resolves a single handler per request type,
    // so a second registration for the same handler interface is a configuration mistake that would
    // otherwise surface as an opaque "multiple services registered" error at dispatch time.
    private static void GuardAgainstDuplicateHandler(IServiceCollection services, Type handlerInterface, Type implementationType)
    {
        foreach (var existing in services)
        {
            if (existing.ServiceType != handlerInterface) continue;

            var existingImplementation = existing.ImplementationType;
            if (existingImplementation is null || existingImplementation == implementationType) continue;

            throw new InvalidOperationException(
                $"Two handlers are registered for '{handlerInterface}': '{existingImplementation}' and '{implementationType}'. A request may have only one handler.");
        }
    }
}
