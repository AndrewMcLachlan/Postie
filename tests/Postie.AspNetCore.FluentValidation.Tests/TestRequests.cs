using FluentValidation;
using Postie.Cqrs.Commands;

namespace Postie.AspNetCore.FluentValidation.Tests;

public record Widget(int Id, string Name);

public record CreateWidget(string Name) : ICommand<Widget>;

public class CreateWidgetHandler : ICommandHandler<CreateWidget, Widget>
{
    public ValueTask<Widget> Handle(CreateWidget command, CancellationToken cancellationToken) =>
        new(new Widget(42, command.Name));
}

public class CreateWidgetValidator : AbstractValidator<CreateWidget>
{
    public CreateWidgetValidator() => RuleFor(c => c.Name).NotEmpty().MaximumLength(10);
}

// A command whose handler always throws a non-validation exception.
public record ExplodeWidget(string Name) : ICommand<Widget>;

public class ExplodeWidgetHandler : ICommandHandler<ExplodeWidget, Widget>
{
    public ValueTask<Widget> Handle(ExplodeWidget command, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("boom");
}

// A command failing multiple rules across multiple properties, proving errors group per property.
public record ShipWidget(string Name, int Quantity) : ICommand<Widget>;

public class ShipWidgetHandler : ICommandHandler<ShipWidget, Widget>
{
    public ValueTask<Widget> Handle(ShipWidget command, CancellationToken cancellationToken) =>
        new(new Widget(1, command.Name));
}

public class ShipWidgetValidator : AbstractValidator<ShipWidget>
{
    public ShipWidgetValidator()
    {
        RuleFor(c => c.Name).NotEmpty().Matches("^[A-Z]");
        RuleFor(c => c.Quantity).GreaterThan(0);
    }
}
