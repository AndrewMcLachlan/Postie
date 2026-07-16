# Postie.AspNetCore

Map CQRS commands and queries directly to ASP.NET Core minimal API endpoints — `MapQuery`, `MapCommand`,
`MapPostCreate` and friends — with the right status codes, `Location` headers, OpenAPI metadata and full
control over request binding. **Mediator-agnostic:** the engine dispatches through a single abstraction,
so you bring your own mediator. Part of [Postie](https://github.com/AndrewMcLachlan/Postie) — **free
forever**, MIT licensed.

```
dotnet add package Postie.AspNetCore
```

This package is the endpoint engine. Pair it with an adapter that registers an `IEndpointDispatcher`:

- [Postie.Cqrs.AspNetCore](https://www.nuget.org/packages/Postie.Cqrs.AspNetCore) — Postie's own mediator.
- [Postie.AspNetCore.MediatR](https://www.nuget.org/packages/Postie.AspNetCore.MediatR) — MediatR.
- Roll your own by implementing `IEndpointDispatcher` (two methods).

## Mapping

```csharp
using Postie.AspNetCore;

var orders = app.MapGroup("/orders");

orders.MapQuery<GetOrders, IReadOnlyList<Order>>("/");
orders.MapQuery<GetOrder, Order>("/{id}").WithName("GetOrder");
orders.MapPostCreate<CreateOrder, Order>("/", "GetOrder", o => new { id = o.Id });
orders.MapPutCommand<UpdateOrder, Order>("/{id}", binding: RequestBinding.Parameters);
orders.MapDeleteCommand<DeleteOrder>("/{id}");
```

Every method returns the `RouteHandlerBuilder`, so you can chain `.WithName(...)`,
`.RequireAuthorization(...)` and so on.

| Method | Verb | Success |
|--------|------|---------|
| `MapQuery<TQuery, TResponse>` | GET | 200 / 404 on null |
| `MapCommand<TCommand, TResponse>` / `MapCommand<TCommand>` | POST | 200 / 204 |
| `MapPutCommand<TCommand, TResponse>` / `MapPutCommand<TCommand>` | PUT | 200 / 204 |
| `MapPatchCommand<TCommand, TResponse>` | PATCH | 200 |
| `MapPostCreate<TCommand, TResponse>` / `MapPutCreate<...>` | POST / PUT | 201 + `Location` |
| `MapDeleteCommand<TCommand>` / `MapDeleteCommand<TCommand, TResponse>` | DELETE | 204 / 200 |
| `MapStreamQuery<TQuery, TResponse>` | GET | 200 (streamed) |

Pass a status code to override the default (e.g. `MapCommand<Submit, Receipt>("/submit", StatusCodes.Status202Accepted)`).

`MapStreamQuery` maps a streaming query (returning `IAsyncEnumerable<TResponse>`) and requires an
`IStreamEndpointDispatcher` — a separate, optional companion to `IEndpointDispatcher` that the Postie and
MediatR adapters register automatically. A roll-your-own mediator only needs to implement it to map streams.

## Not found

Queries returning a reference type respond **404 Not Found** when the dispatched result is null, and
advertise the 404 in OpenAPI metadata. Value-type queries can't be null and don't advertise it.
Collection queries should return empty collections, not null.

## Request binding

Queries always bind from route, query and header values. Command methods take a `RequestBinding` that
defaults to **`Body`** for POST/PUT/PATCH and **`Parameters`** for DELETE:

```csharp
// Default: whole command from the JSON body.
orders.MapCommand<CreateOrder, Order>("/");

// Hybrid: id from the route, payload from the body.
public record UpdateOrder([FromRoute] int Id, [FromBody] OrderDetails Details) : ICommand<Order>;
orders.MapPutCommand<UpdateOrder, Order>("/{id}", binding: RequestBinding.Parameters);
```

Mapping a `Body`-bound command whose members carry `[FromRoute]`/`[FromQuery]`/`[FromHeader]`/`[FromForm]`
fails at startup with a message naming the offending members — those attributes only take effect with
`Parameters` binding.

## Errors

This package does not translate exceptions — handlers throw, and your exception handling maps them to
responses. Postie's mappings only advertise the responses they produce themselves; chain
`.Produces(...)`/`.ProducesValidationProblem()` for responses your pipeline adds. For FluentValidation,
[Postie.AspNetCore.FluentValidation](https://www.nuget.org/packages/Postie.AspNetCore.FluentValidation)
maps `ValidationException` to an RFC 9457 400 problem-details response; or register your own
`IExceptionHandler`.

## License

MIT. See [LICENSE](https://github.com/AndrewMcLachlan/Postie/blob/main/LICENSE).
