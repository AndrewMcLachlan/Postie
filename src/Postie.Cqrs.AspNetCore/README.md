# Postie.Cqrs.AspNetCore

The native adapter that connects [Postie.AspNetCore](https://www.nuget.org/packages/Postie.AspNetCore)'s
endpoint mapping to [Postie.Cqrs](https://www.nuget.org/packages/Postie.Cqrs)'s mediator. Reference this
package to map your `IQuery`/`ICommand` types straight to minimal API endpoints. Part of
[Postie](https://github.com/AndrewMcLachlan/Postie) — **free forever**, MIT licensed.

```
dotnet add package Postie.Cqrs.AspNetCore
```

## Usage

```csharp
using Postie.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Registers Postie's command/query handlers AND the endpoint dispatcher in one call.
builder.Services.AddPostie<GetOrder>();

var app = builder.Build();

var orders = app.MapGroup("/orders");
orders.MapQuery<GetOrder, Order>("/{id}").WithName("GetOrder");
orders.MapPostCreate<CreateOrder, Order>("/", "GetOrder", o => new { id = o.Id });
orders.MapDeleteCommand<DeleteOrder>("/{id}");

app.Run();
```

`AddPostie(...)` is `AddCqrs(...)` plus the endpoint dispatcher. If you register handlers separately, call
`AddPostieEndpointDispatcher()` instead.

The `MapQuery`/`MapCommand`/`MapPostCreate`/… methods and the `RequestBinding` options come from
[Postie.AspNetCore](https://www.nuget.org/packages/Postie.AspNetCore) — see its README for the full surface.

## License

MIT. See [LICENSE](https://github.com/AndrewMcLachlan/Postie/blob/main/LICENSE).
