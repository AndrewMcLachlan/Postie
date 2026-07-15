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
