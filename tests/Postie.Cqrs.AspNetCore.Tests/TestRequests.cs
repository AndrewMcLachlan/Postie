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

// A query with a nullable value-type response (null for id 0), for the 404 path and metadata.
public record FindWidgetCount(int Id) : IQuery<int?>;

public class FindWidgetCountHandler : IQueryHandler<FindWidgetCount, int?>
{
    public ValueTask<int?> Handle(FindWidgetCount query, CancellationToken cancellationToken) =>
        new(query.Id == 0 ? null : query.Id);
}

// A create command whose handler transforms the name, so request- and response-derived route values
// differ — proving the request-aware MapPutCreate overload really uses the request.
public record RegisterWidget(string Name) : ICommand<Widget>;

public class RegisterWidgetHandler : ICommandHandler<RegisterWidget, Widget>
{
    public ValueTask<Widget> Handle(RegisterWidget command, CancellationToken cancellationToken) =>
        new(new Widget(7, $"stored-{command.Name}"));
}

// A no-response hybrid command (route id + body payload), mapped only — used by the binding guard tests.
public record ArchiveWidget([FromRoute] int Id, [FromBody] RenameWidgetBody Body) : ICommand;

// A search query with complex criteria, bound from the body for POST/QUERY-verb query endpoints.
// The handler returns null for the term "missing" so the null-to-404 path can be exercised per verb.
public record SearchWidgets(string Term, int Page) : IQuery<Widget>;

public class SearchWidgetsHandler : IQueryHandler<SearchWidgets, Widget>
{
    public ValueTask<Widget> Handle(SearchWidgets query, CancellationToken cancellationToken) =>
        new(query.Term == "missing" ? null : new Widget(query.Page, query.Term));
}

public record SearchCriteria(string Term);

// A hybrid query: category from the route, criteria from the body — for the Parameters-binding
// override on a POST query, and (mapped with Body binding) for the guard tests.
public record SearchWidgetsIn([FromRoute] int CategoryId, [FromBody] SearchCriteria Criteria) : IQuery<Widget>;

public class SearchWidgetsInHandler : IQueryHandler<SearchWidgetsIn, Widget>
{
    public ValueTask<Widget> Handle(SearchWidgetsIn query, CancellationToken cancellationToken) =>
        new(new Widget(query.CategoryId, query.Criteria.Term));
}

// A streaming query with body-bound criteria.
public record StreamMatchingWidgets(int Count, string Prefix) : IStreamQuery<Widget>;

public class StreamMatchingWidgetsHandler : IStreamQueryHandler<StreamMatchingWidgets, Widget>
{
    public async IAsyncEnumerable<Widget> Handle(StreamMatchingWidgets query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var i = 1; i <= query.Count; i++)
        {
            await Task.Yield();
            yield return new Widget(i, $"{query.Prefix} {i}");
        }
    }
}

// A hybrid streaming query, mapped only — used by the binding guard tests.
public record StreamWidgetsIn([FromRoute] int CategoryId, [FromBody] SearchCriteria Criteria) : IStreamQuery<Widget>;
