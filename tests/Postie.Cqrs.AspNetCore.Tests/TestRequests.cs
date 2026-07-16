using System.Runtime.CompilerServices;
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

// A command that returns a response, bound from the body (for POST/PATCH/PUT-create mappings).
public record SubmitWidget(string Name) : ICommand<Widget>;

public class SubmitWidgetHandler : ICommandHandler<SubmitWidget, Widget>
{
    public ValueTask<Widget> Handle(SubmitWidget command, CancellationToken cancellationToken) =>
        new(new Widget(99, command.Name));
}

// A command that returns a response, bound from the route (for the DELETE-with-response mapping).
public record PurgeWidget(int Id) : ICommand<Widget>;

public class PurgeWidgetHandler : ICommandHandler<PurgeWidget, Widget>
{
    public ValueTask<Widget> Handle(PurgeWidget command, CancellationToken cancellationToken) =>
        new(new Widget(command.Id, "purged"));
}

// A streaming query.
public record StreamWidgets(int Count) : IStreamQuery<Widget>;

public class StreamWidgetsHandler : IStreamQueryHandler<StreamWidgets, Widget>
{
    public async IAsyncEnumerable<Widget> Handle(StreamWidgets query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var i = 1; i <= query.Count; i++)
        {
            await Task.Yield();
            yield return new Widget(i, $"Widget {i}");
        }
    }
}

// A query whose handler returns null for a missing widget (id 0).
public record FindWidget(int Id) : IQuery<Widget>;

public class FindWidgetHandler : IQueryHandler<FindWidget, Widget>
{
    public ValueTask<Widget> Handle(FindWidget query, CancellationToken cancellationToken) =>
        new(query.Id == 0 ? null : new Widget(query.Id, "found"));
}

// A query with a value-type response (for OpenAPI metadata assertions).
public record CountWidgets(int Seed) : IQuery<int>;

public class CountWidgetsHandler : IQueryHandler<CountWidgets, int>
{
    public ValueTask<int> Handle(CountWidgets query, CancellationToken cancellationToken) =>
        new(query.Seed);
}

// A create command whose handler transforms the name, so request- and response-derived route values
// differ — proving the request-aware MapPutCreate overload really uses the request.
public record RegisterWidget(string Name) : ICommand<Widget>;

public class RegisterWidgetHandler : ICommandHandler<RegisterWidget, Widget>
{
    public ValueTask<Widget> Handle(RegisterWidget command, CancellationToken cancellationToken) =>
        new(new Widget(7, $"stored-{command.Name}"));
}
