using Postie.AspNetCore;
using Postie.Cqrs.Commands;
using Postie.Cqrs.Queries;

namespace Postie.Cqrs.AspNetCore;

/// <summary>
/// An <see cref="IEndpointDispatcher"/> that routes endpoint requests to Postie's own query and command
/// dispatchers, choosing the dispatcher from the request's marker interface.
/// </summary>
/// <param name="queryDispatcher">The query dispatcher.</param>
/// <param name="commandDispatcher">The command dispatcher.</param>
internal sealed class PostieEndpointDispatcher(IQueryDispatcher queryDispatcher, ICommandDispatcher commandDispatcher) : IEndpointDispatcher
{
    public ValueTask<TResponse> DispatchAsync<TResponse>(object request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request switch
        {
            IQuery<TResponse> query => queryDispatcher.Dispatch(query, cancellationToken),
            ICommand<TResponse> command => commandDispatcher.Dispatch(command, cancellationToken),
            _ => throw new InvalidOperationException(
                $"Request '{request.GetType().Name}' returning '{typeof(TResponse).Name}' is neither an IQuery<{typeof(TResponse).Name}> nor an ICommand<{typeof(TResponse).Name}>. Map it with a query or command type that implements the matching Postie interface."),
        };
    }

    public ValueTask DispatchAsync(object request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request is ICommand command
            ? commandDispatcher.Execute(command, cancellationToken)
            : throw new InvalidOperationException(
                $"Request '{request.GetType().Name}' is not an ICommand. A no-response endpoint must map a command type that implements ICommand.");
    }
}
