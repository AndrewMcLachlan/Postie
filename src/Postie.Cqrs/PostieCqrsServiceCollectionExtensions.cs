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
    private static readonly Type StreamQueryHandlerGenericType = typeof(IStreamQueryHandler<,>);

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
            services.AddStreamQueryHandlers(assembly);
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

    #region Stream queries
    /// <summary>
    /// Adds the stream query dispatcher and registers stream query handlers found in the given assembly.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="assembly">The assembly containing the stream query handlers.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddStreamQueryHandlers(this IServiceCollection services, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        foreach (var type in assembly.DefinedTypes)
        {
            if (!type.IsClass || type.IsAbstract || type.IsGenericTypeDefinition) continue;

            var streamHandlerInterfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == StreamQueryHandlerGenericType);

            foreach (var interfaceType in streamHandlerInterfaces)
            {
                GuardAgainstDuplicateHandler(services, interfaceType, type);
                services.TryAddEnumerable(ServiceDescriptor.Transient(interfaceType, type));
            }
        }

        services.TryAddTransient<IStreamQueryDispatcher, Dispatcher>();

        return services;
    }

    /// <summary>
    /// Adds an individual stream query handler.
    /// </summary>
    /// <typeparam name="THandler">The type of the handler.</typeparam>
    /// <typeparam name="TRequest">The type of the query.</typeparam>
    /// <typeparam name="TResponse">The type of each streamed item.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddStreamQueryHandler<THandler, TRequest, TResponse>(this IServiceCollection services)
        where THandler : class, IStreamQueryHandler<TRequest, TResponse>
        where TRequest : IStreamQuery<TResponse>
    {
        ArgumentNullException.ThrowIfNull(services);

        GuardAgainstDuplicateHandler(services, typeof(IStreamQueryHandler<TRequest, TResponse>), typeof(THandler));
        services.TryAddEnumerable(ServiceDescriptor.Transient<IStreamQueryHandler<TRequest, TResponse>, THandler>());
        services.TryAddTransient<IStreamQueryDispatcher, Dispatcher>();

        return services;
    }
    #endregion

    #region Pipeline behaviors
    /// <summary>
    /// Registers a query pipeline behavior. Behaviors run in registration order, each surrounding the next.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="behaviorType">
    /// The behavior implementation. Usually an open generic (e.g. <c>typeof(LoggingBehavior&lt;,&gt;)</c>)
    /// that applies to every query, or a closed <see cref="IQueryPipelineBehavior{TQuery, TResponse}"/>.
    /// </param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddQueryPipelineBehavior(this IServiceCollection services, Type behaviorType)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(behaviorType);

        // Multiple behaviors are expected, so register with Add rather than TryAdd; the dispatcher runs
        // every registered behavior in order.
        services.AddTransient(typeof(IQueryPipelineBehavior<,>), behaviorType);

        return services;
    }

    /// <summary>
    /// Registers a stream query pipeline behavior. Behaviors run in registration order, each surrounding the next.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="behaviorType">
    /// The behavior implementation. Usually an open generic (e.g. <c>typeof(LoggingBehavior&lt;,&gt;)</c>)
    /// that applies to every stream query, or a closed <see cref="IStreamQueryPipelineBehavior{TQuery, TResponse}"/>.
    /// </param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddStreamQueryPipelineBehavior(this IServiceCollection services, Type behaviorType)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(behaviorType);

        services.AddTransient(typeof(IStreamQueryPipelineBehavior<,>), behaviorType);

        return services;
    }

    /// <summary>
    /// Registers a command pipeline behavior. The command's response arity is inferred from the behavior
    /// type: a two-parameter behavior targets commands that return a response, a one-parameter behavior
    /// targets commands that do not. Behaviors run in registration order, each surrounding the next.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="behaviorType">
    /// The behavior implementation. Usually an open generic (e.g. <c>typeof(LoggingBehavior&lt;,&gt;)</c>
    /// or <c>typeof(AuditBehavior&lt;&gt;)</c>) that applies to every command.
    /// </param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddCommandPipelineBehavior(this IServiceCollection services, Type behaviorType)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(behaviorType);

        var arity = behaviorType.IsGenericTypeDefinition
            ? behaviorType.GetGenericArguments().Length
            : InferClosedCommandBehaviorArity(behaviorType);

        var behaviorInterface = arity == 1
            ? typeof(ICommandPipelineBehavior<>)
            : typeof(ICommandPipelineBehavior<,>);

        services.AddTransient(behaviorInterface, behaviorType);

        return services;
    }

    private static int InferClosedCommandBehaviorArity(Type behaviorType)
    {
        foreach (var i in behaviorType.GetInterfaces())
        {
            if (!i.IsGenericType) continue;
            var definition = i.GetGenericTypeDefinition();
            if (definition == typeof(ICommandPipelineBehavior<,>)) return 2;
            if (definition == typeof(ICommandPipelineBehavior<>)) return 1;
        }

        throw new ArgumentException(
            $"Type '{behaviorType}' does not implement ICommandPipelineBehavior<TCommand, TResponse> or ICommandPipelineBehavior<TCommand>.",
            nameof(behaviorType));
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
