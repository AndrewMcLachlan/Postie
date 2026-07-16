# Postie.AspNetCore.FluentValidation

Turns FluentValidation failures into proper HTTP responses. An `IExceptionHandler` that maps
`FluentValidation.ValidationException` to an RFC 9457 **400 validation problem details** response,
with failure messages grouped by property. Part of
[Postie](https://github.com/AndrewMcLachlan/Postie) — **free forever**, MIT licensed.

```
dotnet add package Postie.AspNetCore.FluentValidation
```

Pairs naturally with [Postie.Cqrs.FluentValidation](https://www.nuget.org/packages/Postie.Cqrs.FluentValidation)
(whose pipeline behaviors throw `ValidationException` before a handler runs), but works with anything
that throws `ValidationException` — including MediatR validation behaviors.

## Usage

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPostie<CreateOrder>();
builder.Services.AddPostieValidation<CreateOrderValidator>();      // validation behaviors (Postie.Cqrs.FluentValidation)
builder.Services.AddPostieValidationExceptionHandler();            // ValidationException -> 400 problem details

var app = builder.Build();
app.UseExceptionHandler();   // required: the handler runs inside ASP.NET Core's exception middleware

app.MapCommand<CreateOrder, Order>("/orders")
   .ProducesValidationProblem();   // advertise the 400 now that something produces it

app.Run();
```

An invalid `CreateOrder` now returns:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Customer": ["'Customer' must not be empty."]
  }
}
```

The handler writes the response directly via `Results.ValidationProblem`, so `CustomizeProblemDetails`
hooks configured on `AddProblemDetails` do not run for these 400s.

Only `ValidationException` is handled; other exceptions flow to the rest of your exception-handling
pipeline unchanged.

## License

MIT. See [LICENSE](https://github.com/AndrewMcLachlan/Postie/blob/main/LICENSE).
