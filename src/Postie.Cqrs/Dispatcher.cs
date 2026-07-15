using System.Collections.Concurrent;
using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using Postie.Cqrs.Commands;
using Postie.Cqrs.Queries;

namespace Postie.Cqrs;

internal class Dispatcher(IServiceProvider serviceProvider) : IQueryDispatcher, ICommandDispatcher
{
    private static readonly Type QueryHandlerGenericType = typeof(IQueryHandler<,>);
    private static readonly Type CommandHandlerGenericType = typeof(ICommandHandler<,>);
    private static readonly Type CommandHandlerVoidGenericType = typeof(ICommandHandler<>);

    private static readonly Type QueryBehaviorGenericType = typeof(IQueryPipelineBehavior<,>);
    private static readonly Type CommandBehaviorGenericType = typeof(ICommandPipelineBehavior<,>);
    private static readonly Type CommandBehaviorVoidGenericType = typeof(ICommandPipelineBehavior<>);

    // Compiled Handle invokers, cached for the process lifetime. Calling Handle through a compiled
    // delegate avoids per-dispatch reflection (MethodInfo.Invoke + argument array) and surfaces a
    // handler's exceptions as their own type rather than a TargetInvocationException.
    private static readonly ConcurrentDictionary<(Type Request, Type Response), (Type HandlerType, Delegate Invoker)> QueryHandlers = new();
    private static readonly ConcurrentDictionary<(Type Request, Type Response), (Type HandlerType, Delegate Invoker)> CommandHandlers = new();
    private static readonly ConcurrentDictionary<Type, (Type HandlerType, Delegate Invoker)> VoidCommandHandlers = new();

    // Compiled behavior invokers, keyed by the closed behavior interface type.
    private static readonly ConcurrentDictionary<Type, Delegate> ResponseBehaviorInvokers = new();
    private static readonly ConcurrentDictionary<Type, Delegate> VoidBehaviorInvokers = new();

    public ValueTask<TResponse> Dispatch<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var queryType = query.GetType();

        var (handlerType, invoker) = QueryHandlers.GetOrAdd(
            (queryType, typeof(TResponse)),
            static key =>
            {
                var handlerType = QueryHandlerGenericType.MakeGenericType(key.Request, key.Response);
                return (handlerType, BuildInvoker<TResponse>(handlerType, key.Request));
            });

        var handler = serviceProvider.GetRequiredService(handlerType);
        var typedInvoker = (Func<object, object, CancellationToken, ValueTask<TResponse>>)invoker;

        return Pipeline(query, queryType, QueryBehaviorGenericType, () => typedInvoker(handler, query, cancellationToken), cancellationToken);
    }

    public ValueTask<TResponse> Dispatch<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var commandType = command.GetType();

        var (handlerType, invoker) = CommandHandlers.GetOrAdd(
            (commandType, typeof(TResponse)),
            static key =>
            {
                var handlerType = CommandHandlerGenericType.MakeGenericType(key.Request, key.Response);
                return (handlerType, BuildInvoker<TResponse>(handlerType, key.Request));
            });

        var handler = serviceProvider.GetRequiredService(handlerType);
        var typedInvoker = (Func<object, object, CancellationToken, ValueTask<TResponse>>)invoker;

        return Pipeline(command, commandType, CommandBehaviorGenericType, () => typedInvoker(handler, command, cancellationToken), cancellationToken);
    }

    public ValueTask Execute(ICommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var commandType = command.GetType();

        // A command that actually returns a response can reach Execute when passed through a variable
        // statically typed as ICommand. Only ICommandHandler<TCommand> is looked up here (never
        // registered for such commands), so fail with an actionable message rather than an opaque
        // "no service registered" error.
        if (Array.Exists(commandType.GetInterfaces(), i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>)))
        {
            throw new InvalidOperationException(
                $"Command '{commandType.Name}' returns a response; dispatch it with Dispatch<TResponse>(ICommand<TResponse>) rather than the void Execute(ICommand) method.");
        }

        var (handlerType, invoker) = VoidCommandHandlers.GetOrAdd(
            commandType,
            static request =>
            {
                var handlerType = CommandHandlerVoidGenericType.MakeGenericType(request);
                return (handlerType, BuildVoidInvoker(handlerType, request));
            });

        var handler = serviceProvider.GetRequiredService(handlerType);
        var typedInvoker = (Func<object, object, CancellationToken, ValueTask>)invoker;

        return VoidPipeline(command, commandType, () => typedInvoker(handler, command, cancellationToken), cancellationToken);
    }

    // Folds any registered behaviors around the terminal handler call. When none are registered the
    // terminal delegate is invoked directly, so the common case allocates no chain.
    private ValueTask<TResponse> Pipeline<TResponse>(object request, Type requestType, Type behaviorGenericType, RequestHandlerDelegate<TResponse> terminal, CancellationToken cancellationToken)
    {
        var behaviorType = behaviorGenericType.MakeGenericType(requestType, typeof(TResponse));
        var behaviors = serviceProvider.GetServices(behaviorType).OfType<object>().ToArray();

        if (behaviors.Length == 0)
        {
            return terminal();
        }

        var invoker = (Func<object, object, RequestHandlerDelegate<TResponse>, CancellationToken, ValueTask<TResponse>>)
            ResponseBehaviorInvokers.GetOrAdd(behaviorType, static (bt, rt) => BuildResponseBehaviorInvoker<TResponse>(bt, rt), requestType);

        var next = terminal;
        for (var i = behaviors.Length - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var captured = next;
            next = () => invoker(behavior, request, captured, cancellationToken);
        }

        return next();
    }

    private ValueTask VoidPipeline(object request, Type requestType, RequestHandlerDelegate terminal, CancellationToken cancellationToken)
    {
        var behaviorType = CommandBehaviorVoidGenericType.MakeGenericType(requestType);
        var behaviors = serviceProvider.GetServices(behaviorType).OfType<object>().ToArray();

        if (behaviors.Length == 0)
        {
            return terminal();
        }

        var invoker = (Func<object, object, RequestHandlerDelegate, CancellationToken, ValueTask>)
            VoidBehaviorInvokers.GetOrAdd(behaviorType, static (bt, rt) => BuildVoidBehaviorInvoker(bt, rt), requestType);

        var next = terminal;
        for (var i = behaviors.Length - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var captured = next;
            next = () => invoker(behavior, request, captured, cancellationToken);
        }

        return next();
    }

    private static Func<object, object, CancellationToken, ValueTask<TResponse>> BuildInvoker<TResponse>(Type handlerType, Type requestType)
    {
        var (handlerParam, requestParam, cancellationTokenParam, call) = BuildHandleCall(handlerType, requestType);
        return Expression.Lambda<Func<object, object, CancellationToken, ValueTask<TResponse>>>(call, handlerParam, requestParam, cancellationTokenParam).Compile();
    }

    private static Func<object, object, CancellationToken, ValueTask> BuildVoidInvoker(Type handlerType, Type requestType)
    {
        var (handlerParam, requestParam, cancellationTokenParam, call) = BuildHandleCall(handlerType, requestType);
        return Expression.Lambda<Func<object, object, CancellationToken, ValueTask>>(call, handlerParam, requestParam, cancellationTokenParam).Compile();
    }

    private static (ParameterExpression Handler, ParameterExpression Request, ParameterExpression CancellationToken, MethodCallExpression Call) BuildHandleCall(Type handlerType, Type requestType)
    {
        var handleMethod = handlerType.GetMethod("Handle")!;
        var handlerParam = Expression.Parameter(typeof(object), "handler");
        var requestParam = Expression.Parameter(typeof(object), "request");
        var cancellationTokenParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

        var call = Expression.Call(
            Expression.Convert(handlerParam, handlerType),
            handleMethod,
            Expression.Convert(requestParam, requestType),
            cancellationTokenParam);

        return (handlerParam, requestParam, cancellationTokenParam, call);
    }

    private static Func<object, object, RequestHandlerDelegate<TResponse>, CancellationToken, ValueTask<TResponse>> BuildResponseBehaviorInvoker<TResponse>(Type behaviorType, Type requestType)
    {
        var handleMethod = behaviorType.GetMethod("Handle")!;
        var behaviorParam = Expression.Parameter(typeof(object), "behavior");
        var requestParam = Expression.Parameter(typeof(object), "request");
        var nextParam = Expression.Parameter(typeof(RequestHandlerDelegate<TResponse>), "next");
        var cancellationTokenParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

        var call = Expression.Call(
            Expression.Convert(behaviorParam, behaviorType),
            handleMethod,
            Expression.Convert(requestParam, requestType),
            nextParam,
            cancellationTokenParam);

        return Expression.Lambda<Func<object, object, RequestHandlerDelegate<TResponse>, CancellationToken, ValueTask<TResponse>>>(
            call, behaviorParam, requestParam, nextParam, cancellationTokenParam).Compile();
    }

    private static Func<object, object, RequestHandlerDelegate, CancellationToken, ValueTask> BuildVoidBehaviorInvoker(Type behaviorType, Type requestType)
    {
        var handleMethod = behaviorType.GetMethod("Handle")!;
        var behaviorParam = Expression.Parameter(typeof(object), "behavior");
        var requestParam = Expression.Parameter(typeof(object), "request");
        var nextParam = Expression.Parameter(typeof(RequestHandlerDelegate), "next");
        var cancellationTokenParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

        var call = Expression.Call(
            Expression.Convert(behaviorParam, behaviorType),
            handleMethod,
            Expression.Convert(requestParam, requestType),
            nextParam,
            cancellationTokenParam);

        return Expression.Lambda<Func<object, object, RequestHandlerDelegate, CancellationToken, ValueTask>>(
            call, behaviorParam, requestParam, nextParam, cancellationTokenParam).Compile();
    }
}
