# Postie 📬

**Deliver commands and queries straight to ASP.NET Core minimal API endpoints.**

Postie maps CQRS requests to REST endpoints — `MapQuery`, `MapCommand`, `MapPostCreate` and friends —
with the right status codes, `Location` headers, OpenAPI metadata and full control over request binding.
Bring your own mediator: use the lightweight Postie mediator, plug in MediatR, or roll your own by
implementing a single interface.

**Free forever.** MIT licensed. No commercial edition, no license keys, no revenue thresholds.

[![Build and Publish](https://github.com/AndrewMcLachlan/Postie/actions/workflows/build.yml/badge.svg)](https://github.com/AndrewMcLachlan/Postie/actions/workflows/build.yml)

## Quickstart

```
dotnet add package Postie.Cqrs.AspNetCore
```

```csharp
using Postie.AspNetCore;
using Postie.Cqrs.Queries;
using Postie.Cqrs.Commands;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddPostie(typeof(GetOrder).Assembly);   // handlers + endpoint dispatcher

var app = builder.Build();

var orders = app.MapGroup("/orders");
orders.MapQuery<GetOrder, Order>("/{id}").WithName("GetOrder");
orders.MapPostCreate<CreateOrder, Order>("/", "GetOrder", o => new { id = o.Id });
orders.MapPutCommand<UpdateOrder, Order>("/{id}");
orders.MapDeleteCommand<DeleteOrder>("/{id}");

app.Run();

// Requests and handlers
public record GetOrder(int Id) : IQuery<Order>;
public class GetOrderHandler : IQueryHandler<GetOrder, Order>
{
    public ValueTask<Order> Handle(GetOrder query, CancellationToken ct) => ValueTask.FromResult(new Order(query.Id));
}

public record CreateOrder(string Customer) : ICommand<Order>;
public record UpdateOrder([FromRoute] int Id, [FromBody] OrderDetails Details) : ICommand<Order>;
public record DeleteOrder(int Id) : ICommand;
```

No handler lambdas, no boilerplate between the route and your handler.

## Packages

| Package | Description |
|---------|-------------|
| [`Postie.Cqrs`](src/Postie.Cqrs) | The lightweight CQRS mediator: separate command and query dispatchers, pipeline behaviors |
| [`Postie.AspNetCore`](src/Postie.AspNetCore) | The endpoint mapping engine — mediator-agnostic |
| [`Postie.Cqrs.AspNetCore`](src/Postie.Cqrs.AspNetCore) | Adapter wiring the engine to Postie's own mediator |
| [`Postie.AspNetCore.MediatR`](src/Postie.AspNetCore.MediatR) | Adapter for MediatR — keep your existing `IRequest` types |
| [`Postie.Cqrs.FluentValidation`](src/Postie.Cqrs.FluentValidation) | FluentValidation pipeline behaviors |

## Bring your own mediator

The endpoint engine dispatches through one small abstraction, `IEndpointDispatcher`, so the same `Map*`
methods work with any mediator:

**Postie's mediator** (`Postie.Cqrs.AspNetCore`):
```csharp
builder.Services.AddPostie(typeof(GetOrder).Assembly);
```

**MediatR** (`Postie.AspNetCore.MediatR`) — keep your `IRequest` types:
```csharp
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetOrder).Assembly));
builder.Services.AddPostieMediatR();
```

**Roll your own** — implement two methods:
```csharp
public class MyEndpointDispatcher(IMyMediator mediator) : IEndpointDispatcher
{
    public ValueTask<TResponse> DispatchAsync<TResponse>(object request, CancellationToken ct) => /* ... */;
    public ValueTask DispatchAsync(object request, CancellationToken ct) => /* ... */;
}
builder.Services.AddTransient<IEndpointDispatcher, MyEndpointDispatcher>();
```

## Endpoint conventions

| Method | Verb | Success | Default binding |
|--------|------|---------|-----------------|
| `MapQuery<TQuery, TResponse>` | GET | 200 | route/query |
| `MapCommand<TCommand, TResponse>` / `MapCommand<TCommand>` | POST | 200 / 204 | body |
| `MapPutCommand<TCommand, TResponse>` / `MapPutCommand<TCommand>` | PUT | 200 / 204 | body |
| `MapPatchCommand<TCommand, TResponse>` | PATCH | 200 | body |
| `MapPostCreate<TCommand, TResponse>` / `MapPutCreate<...>` | POST / PUT | 201 + `Location` | body |
| `MapDeleteCommand<TCommand>` / `MapDeleteCommand<TCommand, TResponse>` | DELETE | 204 / 200 | route/query |
| `MapStreamQuery<TQuery, TResponse>` | GET | 200 (streamed) | route/query |

Command methods take a `RequestBinding` (`Body`, `Parameters`, or `Default`) to override the default —
use `Parameters` for hybrid endpoints that bind an id from the route and a payload from the body.

Streaming queries (`IStreamQuery<TResponse>` returning `IAsyncEnumerable<TResponse>`) map with
`MapStreamQuery` and stream their results as they are produced.

## Pipeline behaviors and validation

Cross-cutting concerns wrap handling through CQS-split behaviors (`IQueryPipelineBehavior<,>`,
`ICommandPipelineBehavior<,>`, `ICommandPipelineBehavior<>`):

```csharp
builder.Services.AddQueryPipelineBehavior(typeof(TimingBehavior<,>));
```

For FluentValidation, [`Postie.Cqrs.FluentValidation`](src/Postie.Cqrs.FluentValidation) ships ready-made
behaviors:

```csharp
builder.Services.AddPostieValidation(typeof(CreateOrder).Assembly);
```

## OpenTelemetry

The dispatcher records an activity per query, command and stream query. Add the source to your tracer:

```csharp
builder.Services.AddOpenTelemetry().WithTracing(t => t.AddSource(PostieDiagnostics.ActivitySourceName));
```

Tracing costs nothing until you opt in — with no listener the dispatch takes an allocation-free fast path.

## Error handling

Postie's endpoint layer doesn't translate exceptions — handlers throw, and your exception handling maps
them to responses. Pair with [Asm.AspNetCore](https://www.nuget.org/packages/Asm.AspNetCore), which maps
common exception types (including FluentValidation's `ValidationException`) to RFC 9457 problem details, or
register your own `IExceptionHandler`.

## Why another one?

The endpoint layer is the point. Most mediator libraries stop at dispatch; Postie's focus is the last mile
between HTTP and your handlers, done REST-properly (201 + `Location` via named routes, 204 for no-content
commands, verb-appropriate binding defaults) — and it works with whatever mediator you already use.

## Building

```
dotnet build Postie.slnx
dotnet test Postie.slnx
```

Targets `net8.0`, `net9.0` and `net10.0`.

## License

MIT. See [LICENSE](LICENSE).
