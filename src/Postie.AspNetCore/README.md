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
| `MapQuery<TQuery, TResponse>` | GET (default) / POST / QUERY | 200 / 404 on null |
| `MapCommand<TCommand, TResponse>` / `MapCommand<TCommand>` | POST | 200 / 204 |
| `MapPutCommand<TCommand, TResponse>` / `MapPutCommand<TCommand>` | PUT | 200 / 204 |
| `MapPatchCommand<TCommand, TResponse>` | PATCH | 200 |
| `MapPostCreate<TCommand, TResponse>` / `MapPutCreate<...>` | POST / PUT | 201 + `Location` |
| `MapDeleteCommand<TCommand>` / `MapDeleteCommand<TCommand, TResponse>` | DELETE | 204 / 200 |
| `MapStreamQuery<TQuery, TResponse>` | GET (default) / POST / QUERY | 200 (streamed) |

Pass a status code to override the default (e.g. `MapCommand<Submit, Receipt>("/submit", StatusCodes.Status202Accepted)`).

`MapStreamQuery` maps a streaming query (returning `IAsyncEnumerable<TResponse>`) and requires an
`IStreamEndpointDispatcher` — a separate, optional companion to `IEndpointDispatcher` that the Postie and
MediatR adapters register automatically. A roll-your-own mediator only needs to implement it to map streams.

`MapStreamQuery` responses are written as a JSON array serialised incrementally — the first items
reach the client while later ones are still being produced, and memory stays flat however long the
stream runs. Because the 200 status line is sent before enumeration finishes, a handler failure
mid-stream aborts the connection with truncated JSON rather than producing an error status.

### Queries with a request body

Criteria too rich for a query string — nested filters, sort sets, paging — can bind from the body
by picking the HTTP method for the query. POST and the HTTP QUERY method bind the query from the
request body by default; an explicit `RequestBinding` overrides any default:

```csharp
public record SearchOrders(OrderFilter Filter, Paging Page, Sort[] Sort) : IQuery<PagedResult<Order>>;

app.MapQuery<SearchOrders, PagedResult<Order>>("/orders/search", QueryMethod.Post);
app.MapQuery<SearchOrders, PagedResult<Order>>("/orders/search", QueryMethod.Query);
```

The response contract does not change with the method: a null result is 404, anything else is
200. The HTTP QUERY method is safe and idempotent like GET while carrying a body like POST —
the semantically ideal fit for a query — but ecosystem support is still maturing:
a spike against `Microsoft.AspNetCore.OpenApi` (10.0.10, on .NET 10) found that a QUERY-mapped
endpoint is silently omitted from the generated `/openapi/v1.json` — document generation succeeds
and the GET and POST endpoints appear correctly, but the QUERY endpoint has no entry at all — and
intermediaries or clients may not recognise the method yet. POST works everywhere today.

## Not found

Queries returning a reference type respond **404 Not Found** when the dispatched result is null, and
advertise the 404 in OpenAPI metadata. Value-type queries can't be null and don't advertise it.
Collection queries should return empty collections, not null.

## Request binding

Queries bind idiomatically for their `QueryMethod` — route, query and header values for GET, the
request body for POST and QUERY — unless an explicit `RequestBinding` overrides that. Command methods
take a `RequestBinding` that defaults to **`Body`** for POST/PUT/PATCH and **`Parameters`** for DELETE:

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
