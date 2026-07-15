# Postie.AspNetCore.MediatR

Dispatch [Postie.AspNetCore](https://www.nuget.org/packages/Postie.AspNetCore) endpoints through MediatR.
Keep your existing `IRequest<TResponse>` types and handlers — Postie gives you the REST-idiomatic endpoint
mapping on top. Part of [Postie](https://github.com/AndrewMcLachlan/Postie) — **free forever**, MIT licensed.

```
dotnet add package Postie.AspNetCore.MediatR
```

> This adapter targets MediatR 12.x (the last Apache-2.0 line) as its floor, so it stays free by default.
> If your app references MediatR 13+, NuGet resolves to your version.

## Usage

```csharp
using Postie.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Register MediatR as usual...
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetOrder).Assembly));
// ...then wire Postie's endpoints to it.
builder.Services.AddPostieMediatR();

var app = builder.Build();

// Plain MediatR requests — no Postie interfaces required.
app.MapQuery<GetOrder, Order>("/orders/{id}").WithName("GetOrder");
app.MapPostCreate<CreateOrder, Order>("/orders", "GetOrder", o => new { id = o.Id });
app.MapDeleteCommand<DeleteOrder>("/orders/{id}");

app.Run();

public record GetOrder(int Id) : IRequest<Order>;
public record CreateOrder(string Customer) : IRequest<Order>;
public record DeleteOrder(int Id) : IRequest;   // no-response request
```

The `MapQuery`/`MapCommand`/`MapPostCreate`/… methods and the `RequestBinding` options come from
[Postie.AspNetCore](https://www.nuget.org/packages/Postie.AspNetCore) — see its README for the full surface.

## License

MIT. See [LICENSE](https://github.com/AndrewMcLachlan/Postie/blob/main/LICENSE).
