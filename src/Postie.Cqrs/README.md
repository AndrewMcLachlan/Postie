# Postie.Cqrs

A lightweight CQRS mediator for .NET. Commands and queries are separate, first-class concepts with their
own handler and dispatcher interfaces, dispatched through cached compiled delegates (no per-call
reflection). Part of [Postie](https://github.com/AndrewMcLachlan/Postie) — **free forever**, MIT licensed.

```
dotnet add package Postie.Cqrs
```

## Define requests and handlers

```csharp
using Postie.Cqrs.Queries;
using Postie.Cqrs.Commands;

public record Order(int Id);

public record GetOrder(int Id) : IQuery<Order>;

public class GetOrderHandler : IQueryHandler<GetOrder, Order>
{
    public ValueTask<Order> Handle(GetOrder query, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new Order(query.Id));
}

public record CreateOrder(string Customer) : ICommand<Order>;   // command that returns a response
public record DeleteOrder(int Id) : ICommand;                   // command with no response
```

Command handlers implement `ICommandHandler<TCommand, TResponse>` or `ICommandHandler<TCommand>`.

## Register

```csharp
builder.Services.AddCqrs<GetOrder>();                  // scans the marker type's assembly
builder.Services.AddCqrs(typeof(GetOrder).Assembly);   // or pass assemblies explicitly
```

At least one assembly is required — there is no calling-assembly fallback. Or register handlers
individually with `AddQueryHandler<,,>` / `AddCommandHandler<,,>` / `AddCommandHandler<,>`.

## Dispatch

```csharp
public class OrderService(IQueryDispatcher queries, ICommandDispatcher commands)
{
    public ValueTask<Order> Get(int id, CancellationToken ct) => queries.Dispatch(new GetOrder(id), ct);
    public ValueTask<Order> Create(string customer, CancellationToken ct) => commands.Dispatch(new CreateOrder(customer), ct);
    public ValueTask Delete(int id, CancellationToken ct) => commands.Execute(new DeleteOrder(id), ct);
}
```

Commands that return a response are dispatched with `Dispatch`; those that do not are run with `Execute`.

## Pipeline behaviors

Wrap handling with cross-cutting concerns. The query and command pipelines are separate, so a behavior can
target one side without touching the other.

```csharp
public class TimingBehavior<TQuery, TResponse>(ILogger<TQuery> logger) : IQueryPipelineBehavior<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    public async ValueTask<TResponse> Handle(TQuery query, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var response = await next();
        logger.LogInformation("{Query} took {Elapsed}ms", typeof(TQuery).Name, sw.ElapsedMilliseconds);
        return response;
    }
}

builder.Services.AddQueryPipelineBehavior(typeof(TimingBehavior<,>));
```

Use `AddCommandPipelineBehavior` for command behaviors (`ICommandPipelineBehavior<TCommand, TResponse>` or
`ICommandPipelineBehavior<TCommand>`). Behaviors run in registration order, each surrounding the next.

## Streaming queries

For queries that return a stream of results, implement `IStreamQuery<TResponse>` and
`IStreamQueryHandler<TQuery, TResponse>` (returning `IAsyncEnumerable<TResponse>`) and dispatch through
`IStreamQueryDispatcher`:

```csharp
public record Tail(string File) : IStreamQuery<string>;

public class TailHandler : IStreamQueryHandler<Tail, string>
{
    public async IAsyncEnumerable<string> Handle(Tail query, [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var line in ReadLines(query.File, ct)) yield return line;
    }
}
```

Stream queries have their own pipeline behaviors (`IStreamQueryPipelineBehavior<TQuery, TResponse>`,
registered with `AddStreamQueryPipelineBehavior`) and are mapped to endpoints with `MapStreamQuery`.

## OpenTelemetry

The dispatcher records an activity per query, command and stream query (tagged with the request kind and
type). Add the source to your tracer to collect them:

```csharp
builder.Services.AddOpenTelemetry().WithTracing(t => t.AddSource(PostieDiagnostics.ActivitySourceName));
```

When nothing is listening the dispatch takes an allocation-free fast path, so tracing costs nothing until
you opt in.

For streaming queries the activity starts when enumeration begins (not at dispatch) and parents to the
enumeration-time `Activity.Current`, so an unenumerated stream records nothing.

For ready-made FluentValidation behaviors see [Postie.Cqrs.FluentValidation](https://www.nuget.org/packages/Postie.Cqrs.FluentValidation).

## Map to minimal API endpoints

[Postie.Cqrs.AspNetCore](https://www.nuget.org/packages/Postie.Cqrs.AspNetCore) maps these commands and
queries straight to ASP.NET Core minimal API endpoints.

## License

MIT. See [LICENSE](https://github.com/AndrewMcLachlan/Postie/blob/main/LICENSE).
