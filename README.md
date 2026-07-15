# Postie 📬

**Deliver commands and queries straight to ASP.NET Core minimal API endpoints.**

Postie maps CQRS requests to REST endpoints — `MapQuery`, `MapCommand`, `MapPostCreate` and friends —
with the right status codes, `Location` headers, OpenAPI metadata and full control over request binding.
Bring your own mediator: use the lightweight Postie dispatcher, plug in MediatR, or roll your own by
implementing a single interface.

**Free forever.** MIT licensed. No commercial edition, no license keys, no revenue thresholds.

> 🚧 Postie is under construction ahead of its first beta release. The API may change without notice
> until 1.0.

## A taste

```csharp
var orders = app.MapGroup("/orders");

orders.MapQuery<GetOrders, IEnumerable<Order>>("/");
orders.MapQuery<GetOrder, Order>("/{id}").WithName("GetOrder");
orders.MapPostCreate<CreateOrder, Order>("/", routeName: "GetOrder", o => new { id = o.Id });
orders.MapPutCommand<UpdateOrder, Order>("/{id}");
orders.MapDeleteCommand<DeleteOrder>("/{id}");
```

No handler lambdas, no boilerplate between the route and your handler — and hybrid binding
(`[FromRoute] int Id` alongside a `[FromBody]` payload) that answers the first question everyone
asks with this pattern.

## Packages

| Package | Description |
|---------|-------------|
| `Postie.Cqrs` | A lightweight CQRS mediator: separate command and query dispatchers, pipeline behaviors, streaming, OpenTelemetry |
| `Postie.AspNetCore` | The endpoint mapping engine — mediator-agnostic |
| `Postie.Cqrs.AspNetCore` | Postie.Cqrs adapter with strongly typed mapping methods |
| `Postie.AspNetCore.MediatR` | MediatR adapter — keep your existing `IRequest` types |
| `Postie.Cqrs.FluentValidation` | FluentValidation pipeline behavior |

## Why Postie?

- **The endpoint layer is the point.** Most mediator libraries stop at dispatch; Postie's focus is the
  last mile between HTTP and your handlers, done REST-properly (201 + `Location` via named routes,
  204 for no-content commands, verb-appropriate binding defaults).
- **Mediator-agnostic by design.** The mapping engine dispatches through one small abstraction.
  Postie's own mediator is in the box, MediatR is an adapter package away, and roll-your-own is one
  interface.
- **CQS-split, not one `IRequest`.** Commands and queries are different things with different
  dispatchers and different pipelines.

## License

MIT. See [LICENSE](LICENSE).
