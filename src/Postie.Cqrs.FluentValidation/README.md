# Postie.Cqrs.FluentValidation

FluentValidation pipeline behaviors for [Postie.Cqrs](https://www.nuget.org/packages/Postie.Cqrs).
Validate commands and queries before they reach the handler; a failure throws
`FluentValidation.ValidationException`. Part of [Postie](https://github.com/AndrewMcLachlan/Postie) —
**free forever**, MIT licensed.

```
dotnet add package Postie.Cqrs.FluentValidation
```

## Usage

Write validators as usual, then register them and the behaviors in one call:

```csharp
using FluentValidation;

public record CreateOrder(string Customer) : ICommand<Order>;

public class CreateOrderValidator : AbstractValidator<CreateOrder>
{
    public CreateOrderValidator() => RuleFor(c => c.Customer).NotEmpty();
}

// Program.cs
builder.Services.AddCqrs<CreateOrder>();
builder.Services.AddPostieValidation<CreateOrder>();   // scans validators + wires behaviors
```

Now dispatching a `CreateOrder` with an empty `Customer` throws `ValidationException` before the handler
runs. Both queries and commands (with or without a response) are validated.

## Turning failures into responses

The behaviors throw; your exception handling turns that into an HTTP response. In an ASP.NET Core app,
pair with [Postie.AspNetCore.FluentValidation](https://www.nuget.org/packages/Postie.AspNetCore.FluentValidation),
whose exception handler maps `ValidationException` to an RFC 9457 400 problem-details response — or
register your own `IExceptionHandler`.

## License

MIT. See [LICENSE](https://github.com/AndrewMcLachlan/Postie/blob/main/LICENSE).
