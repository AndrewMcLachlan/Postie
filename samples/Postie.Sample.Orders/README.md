# Postie.Sample.Orders

A minimal Orders API on Postie's built-in CQRS mediator, showing the endpoint conventions:

- `GET /orders` — `MapQuery`, 200 with a list (empty list, never null)
- `GET /orders/{id}` — `MapQuery` with a nullable response: 200, or 404 when the handler returns null
- `POST /orders` — `MapPostCreate`, 201 with a `Location` header built from the `GetOrder` route
- `PUT /orders/{id}` — `MapPutCommand` with `RequestBinding.Parameters`: id from the route, payload from the body
- `DELETE /orders/{id}` — `MapDeleteCommand`, 204

Also wired: OpenTelemetry tracing of Postie's dispatch — every request prints a `Postie <RequestType>`
span to the console via the console exporter.

## Run

```
dotnet run --project samples/Postie.Sample.Orders
```

Then use [Postie.Sample.Orders.http](Postie.Sample.Orders.http) (VS / Rider / VS Code REST Client)
against `http://localhost:5199`, and watch the spans appear in the console.

Storage is in-memory and seeded with three orders. Unhandled exceptions surface as 500s — in a real
app, register your own `IExceptionHandler`.
