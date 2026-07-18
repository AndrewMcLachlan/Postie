using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Postie.Cqrs.Commands;
using Postie.Cqrs.Queries;

namespace Postie.Cqrs;

internal class Dispatcher(IServiceProvider serviceProvider) : IQueryDispatcher, ICommandDispatcher, IStreamQueryDispatcher
{
    private static readonly Type QueryHandlerGenericType = typeof(IQueryHandler<,>);
    private static readonly Type CommandHandlerGenericType = typeof(ICommandHandler<,>);
    private static readonly Type CommandHandlerVoidGenericType = typeof(ICommandHandler<>);
    private static readonly Type StreamQueryHandlerGenericType = typeof(IStreamQueryHandler<,>);

    private static readonly Type QueryBehaviorGenericType = typeof(IQueryPipelineBehavior<,>);
    private static readonly Type CommandBehaviorGenericType = typeof(ICommandPipelineBehavior<,>);
    private static readonly Type CommandBehaviorVoidGenericType = typeof(ICommandPipelineBehavior<>);
    private static readonly Type StreamQueryBehaviorGenericType = typeof(IStreamQueryPipelineBehavior<,>);

    // Compiled Handle invokers, cached for the process lifetime. Calling Handle through a compiled
    // delegate avoids per-dispatch reflection (MethodInfo.Invoke + argument array) and surfaces a
    // handler's exceptions as their own type rather than a TargetInvocationException.
    private static readonly ConcurrentDictionary<(Type Request, Type Response), (Type HandlerType, Delegate Invoker)> QueryHandlers = new();
    private static readonly ConcurrentDictionary<(Type Request, Type Response), (Type HandlerType, Delegate Invoker)> CommandHandlers = new();
    private static readonly ConcurrentDictionary<Type, (Type HandlerType, Delegate Invoker)> VoidCommandHandlers = new();
    private static readonly ConcurrentDictionary<(Type Request, Type Response), (Type HandlerType, Delegate Invoker)> StreamQueryHandlers = new();

    // Compiled behavior invokers, keyed by the closed behavior interface type.
    private static readonly ConcurrentDictionary<Type, Delegate> ResponseBehaviorInvokers = new();
    private static readonly ConcurrentDictionary<Type, Delegate> VoidBehaviorInvokers = new();
    private static readonly ConcurrentDictionary<Type, Delegate> StreamBehaviorInvokers = new();

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

        ValueTask<TResponse> Run() => Pipeline(query, queryType, QueryBehaviorGenericType, () => typedInvoker(handler, query, cancellationToken), cancellationToken);

        var activity = StartActivity("query", queryType);
        return activity is null ? Run() : Awaited(activity, Run);
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

        ValueTask<TResponse> Run() => Pipeline(command, commandType, CommandBehaviorGenericType, () => typedInvoker(handler, command, cancellationToken), cancellationToken);

        var activity = StartActivity("command", commandType);
        return activity is null ? Run() : Awaited(activity, Run);
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

        ValueTask Run() => VoidPipeline(command, commandType, () => typedInvoker(handler, command, cancellationToken), cancellationToken);

        var activity = StartActivity("command", commandType);
        return activity is null ? Run() : Awaited(activity, Run);
    }

    public IAsyncEnumerable<TResponse> Dispatch<TResponse>(IStreamQuery<TResponse> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var queryType = query.GetType();

        var (handlerType, invoker) = StreamQueryHandlers.GetOrAdd(
            (queryType, typeof(TResponse)),
            static key =>
            {
                var handlerType = StreamQueryHandlerGenericType.MakeGenericType(key.Request, key.Response);
                return (handlerType, BuildStreamInvoker<TResponse>(handlerType, key.Request));
            });

        var handler = serviceProvider.GetRequiredService(handlerType);
        var typedInvoker = (Func<object, object, CancellationToken, IAsyncEnumerable<TResponse>>)invoker;

        var stream = StreamPipeline(query, queryType, () => typedInvoker(handler, query, cancellationToken), cancellationToken);

        // Enumerate owns the activity; a stream that is never enumerated must not touch
        // Activity.Current. With no listener the pipeline stream is returned untouched.
        return PostieDiagnostics.ActivitySource.HasListeners() ? Enumerate(queryType, stream, cancellationToken) : stream;
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

    private IAsyncEnumerable<TResponse> StreamPipeline<TResponse>(object request, Type requestType, StreamHandlerDelegate<TResponse> terminal, CancellationToken cancellationToken)
    {
        var behaviorType = StreamQueryBehaviorGenericType.MakeGenericType(requestType, typeof(TResponse));
        var behaviors = serviceProvider.GetServices(behaviorType).OfType<object>().ToArray();

        if (behaviors.Length == 0)
        {
            return terminal();
        }

        var invoker = (Func<object, object, StreamHandlerDelegate<TResponse>, CancellationToken, IAsyncEnumerable<TResponse>>)
            StreamBehaviorInvokers.GetOrAdd(behaviorType, static (bt, rt) => BuildStreamBehaviorInvoker<TResponse>(bt, rt), requestType);

        var next = terminal;
        for (var i = behaviors.Length - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var captured = next;
            next = () => invoker(behavior, request, captured, cancellationToken);
        }

        return next();
    }

    // Returns a started activity, or null when nothing is listening (the common case) so the fast path
    // pays only a cheap HasListeners check and no string allocation.
    private static Activity? StartActivity(string kind, Type requestType)
    {
        if (!PostieDiagnostics.ActivitySource.HasListeners())
        {
            return null;
        }

        var activity = PostieDiagnostics.ActivitySource.StartActivity($"Postie {requestType.Name}");
        if (activity is not null)
        {
            activity.SetTag("postie.request_kind", kind);
            activity.SetTag("postie.request_type", requestType.FullName);
        }

        return activity;
    }

    // The pipeline runs inside the try so a synchronous throw from the handler is recorded on the
    // activity (and the activity disposed) rather than escaping before it is stopped.
    private static async ValueTask<TResponse> Awaited<TResponse>(Activity activity, RequestHandlerDelegate<TResponse> pipeline)
    {
        using (activity)
        {
            try
            {
                return await pipeline();
            }
            catch (Exception ex)
            {
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }
    }

    private static async ValueTask Awaited(Activity activity, RequestHandlerDelegate pipeline)
    {
        using (activity)
        {
            try
            {
                await pipeline();
            }
            catch (Exception ex)
            {
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }
    }

    // Owns the stream dispatch activity: it starts on first MoveNextAsync and the 'using' stops it
    // on completion, exception, or early disposal, so an unenumerated stream never touches
    // Activity.Current.
    private static async IAsyncEnumerable<TResponse> Enumerate<TResponse>(Type queryType, IAsyncEnumerable<TResponse> source, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var activity = StartActivity("stream", queryType);
        await using var enumerator = source.GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            bool hasNext;
            try
            {
                hasNext = await enumerator.MoveNextAsync();
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }

            if (!hasNext)
            {
                break;
            }

            yield return enumerator.Current;
        }
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

    private static Func<object, object, CancellationToken, IAsyncEnumerable<TResponse>> BuildStreamInvoker<TResponse>(Type handlerType, Type requestType)
    {
        var (handlerParam, requestParam, cancellationTokenParam, call) = BuildHandleCall(handlerType, requestType);
        return Expression.Lambda<Func<object, object, CancellationToken, IAsyncEnumerable<TResponse>>>(call, handlerParam, requestParam, cancellationTokenParam).Compile();
    }

    private static Func<object, object, StreamHandlerDelegate<TResponse>, CancellationToken, IAsyncEnumerable<TResponse>> BuildStreamBehaviorInvoker<TResponse>(Type behaviorType, Type requestType)
    {
        var handleMethod = behaviorType.GetMethod("Handle")!;
        var behaviorParam = Expression.Parameter(typeof(object), "behavior");
        var requestParam = Expression.Parameter(typeof(object), "request");
        var nextParam = Expression.Parameter(typeof(StreamHandlerDelegate<TResponse>), "next");
        var cancellationTokenParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

        var call = Expression.Call(
            Expression.Convert(behaviorParam, behaviorType),
            handleMethod,
            Expression.Convert(requestParam, requestType),
            nextParam,
            cancellationTokenParam);

        return Expression.Lambda<Func<object, object, StreamHandlerDelegate<TResponse>, CancellationToken, IAsyncEnumerable<TResponse>>>(
            call, behaviorParam, requestParam, nextParam, cancellationTokenParam).Compile();
    }
}
