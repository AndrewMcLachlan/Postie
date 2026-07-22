using System.Runtime.CompilerServices;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Postie.AspNetCore.MediatR.Tests;

// Plain MediatR request types — no Postie interfaces — proving the endpoint engine dispatches through
// whatever mediator is registered.

public record GetGreeting(string Name) : IRequest<string>;

public class GetGreetingHandler : IRequestHandler<GetGreeting, string>
{
    public Task<string> Handle(GetGreeting request, CancellationToken cancellationToken) =>
        Task.FromResult($"Hello, {request.Name}");
}

public record CreateWidget(string Name) : IRequest<Widget>;

public record Widget(int Id, string Name);

public class CreateWidgetHandler : IRequestHandler<CreateWidget, Widget>
{
    public Task<Widget> Handle(CreateWidget request, CancellationToken cancellationToken) =>
        Task.FromResult(new Widget(42, request.Name));
}

public record RenameWidget([FromRoute] int Id, [FromBody] RenameWidgetBody Body) : IRequest<Widget>;

public record RenameWidgetBody(string Name);

public class RenameWidgetHandler : IRequestHandler<RenameWidget, Widget>
{
    public Task<Widget> Handle(RenameWidget request, CancellationToken cancellationToken) =>
        Task.FromResult(new Widget(request.Id, request.Body.Name));
}

public record DeleteWidget(int Id) : IRequest;

public class DeleteWidgetHandler : IRequestHandler<DeleteWidget>
{
    public Task Handle(DeleteWidget request, CancellationToken cancellationToken) => Task.CompletedTask;
}

public record StreamWidgets(int Count) : IStreamRequest<Widget>;

public class StreamWidgetsHandler : IStreamRequestHandler<StreamWidgets, Widget>
{
    public async IAsyncEnumerable<Widget> Handle(StreamWidgets request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var i = 1; i <= request.Count; i++)
        {
            await Task.Yield();
            yield return new Widget(i, $"Widget {i}");
        }
    }
}

// A request whose handler returns null for a missing widget (id 0).
public record FindWidget(int Id) : IRequest<Widget>;

public class FindWidgetHandler : IRequestHandler<FindWidget, Widget>
{
    public Task<Widget> Handle(FindWidget request, CancellationToken cancellationToken) =>
        Task.FromResult(request.Id == 0 ? null : new Widget(request.Id, "found"));
}

// A request that returns a response, bound from the body (for POST/PATCH/PUT-create mappings).
public record SubmitWidget(string Name) : IRequest<Widget>;

public class SubmitWidgetHandler : IRequestHandler<SubmitWidget, Widget>
{
    public Task<Widget> Handle(SubmitWidget request, CancellationToken cancellationToken) =>
        Task.FromResult(new Widget(99, request.Name));
}

// A request that returns a response, bound from the route (for the DELETE-with-response mapping).
public record PurgeWidget(int Id) : IRequest<Widget>;

public class PurgeWidgetHandler : IRequestHandler<PurgeWidget, Widget>
{
    public Task<Widget> Handle(PurgeWidget request, CancellationToken cancellationToken) =>
        Task.FromResult(new Widget(request.Id, "purged"));
}

// A search request with body-bound criteria; null for the term "missing", so the null-to-404
// convention can be exercised on the POST and QUERY verbs.
public record SearchWidgets(string Term, int Page) : IRequest<Widget>;

public class SearchWidgetsHandler : IRequestHandler<SearchWidgets, Widget>
{
    public Task<Widget> Handle(SearchWidgets request, CancellationToken cancellationToken) =>
        Task.FromResult(request.Term == "missing" ? null : new Widget(request.Page, request.Term));
}

// A request whose handler throws, proving exceptions surface unwrapped through the MediatR path.
public record ExplodingRequest(string Reason) : IRequest<Widget>;

public class ExplodingRequestHandler : IRequestHandler<ExplodingRequest, Widget>
{
    public Task<Widget> Handle(ExplodingRequest request, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(request.Reason);
}

// A request with a nullable value-type response (null for id 0), for the 404 path.
public record FindWidgetCount(int Id) : IRequest<int?>;

public class FindWidgetCountHandler : IRequestHandler<FindWidgetCount, int?>
{
    public Task<int?> Handle(FindWidgetCount request, CancellationToken cancellationToken) =>
        Task.FromResult(request.Id == 0 ? (int?)null : request.Id);
}
