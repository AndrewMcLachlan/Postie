# Postie.Sample.Orders.MediatR

The same Orders API as [Postie.Sample.Orders](../Postie.Sample.Orders), dispatched through **MediatR**
instead of Postie's built-in mediator — plus validation, OpenAPI, and streaming:

- **MediatR**: plain `IRequest<T>` types and handlers; Postie only maps the endpoints
  (`AddPostieMediatR()` wires the dispatch).
- **FluentValidation end-to-end**: validators + a sample-local `ValidationBehavior` MediatR pipeline
  behavior that throws `ValidationException`, and `Postie.AspNetCore.FluentValidation`'s exception
  handler mapping it to an RFC 9457 400. (`Postie.Cqrs.FluentValidation` targets Postie's own
  mediator, which is why the MediatR pipeline carries its own behavior.) The create/update endpoints
  chain `.ProducesValidationProblem()` — advertising the 400 because this app actually produces it.
- **OpenAPI + Scalar**: browse the API at [`/scalar`](http://localhost:5299/scalar) and note the
  honest metadata — 404 on `GET /orders/{id}`, 201 on create, 400 only where validation is wired.
- **Streaming**: `GET /orders/stream` maps a MediatR `IStreamRequest` with `MapStreamQuery`; items
  arrive every 500 ms.

## Run

```
dotnet run --project samples/Postie.Sample.Orders.MediatR
```

Then use [Postie.Sample.Orders.MediatR.http](Postie.Sample.Orders.MediatR.http) against
`http://localhost:5299`, or explore interactively at `http://localhost:5299/scalar`.

Storage is in-memory and seeded with three orders.
