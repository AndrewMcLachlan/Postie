using Microsoft.AspNetCore.Mvc;
using Postie.Cqrs.Commands;
using Postie.Cqrs.Queries;

namespace Postie.Cqrs.AspNetCore.Tests;

// A query bound from the route.
public record GetGreeting(string Name) : IQuery<string>;

public class GetGreetingHandler : IQueryHandler<GetGreeting, string>
{
    public ValueTask<string> Handle(GetGreeting query, CancellationToken cancellationToken) =>
        new($"Hello, {query.Name}");
}

// A create command bound from the body.
public record CreateWidget(string Name) : ICommand<Widget>;

public record Widget(int Id, string Name);

public class CreateWidgetHandler : ICommandHandler<CreateWidget, Widget>
{
    public ValueTask<Widget> Handle(CreateWidget command, CancellationToken cancellationToken) =>
        new(new Widget(42, command.Name));
}

// A hybrid command: id from the route, payload from the body.
public record RenameWidget([FromRoute] int Id, [FromBody] RenameWidgetBody Body) : ICommand<Widget>;

public record RenameWidgetBody(string Name);

public class RenameWidgetHandler : ICommandHandler<RenameWidget, Widget>
{
    public ValueTask<Widget> Handle(RenameWidget command, CancellationToken cancellationToken) =>
        new(new Widget(command.Id, command.Body.Name));
}

// A no-response command bound from the route.
public record DeleteWidget(int Id) : ICommand;

public class DeleteWidgetHandler : ICommandHandler<DeleteWidget>
{
    public ValueTask Handle(DeleteWidget command, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
