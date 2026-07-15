using MediatR;
using Postie.AspNetCore;

namespace Postie.AspNetCore.MediatR;

/// <summary>
/// An <see cref="IEndpointDispatcher"/> that routes endpoint requests through MediatR's
/// <see cref="ISender"/>, so Postie's endpoint mapping can be used with existing MediatR
/// <see cref="IRequest{TResponse}"/> types.
/// </summary>
/// <param name="sender">The MediatR sender.</param>
internal sealed class MediatREndpointDispatcher(ISender sender) : IEndpointDispatcher
{
    public ValueTask<TResponse> DispatchAsync<TResponse>(object request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request is not IRequest<TResponse> typedRequest)
        {
            throw new InvalidOperationException(
                $"Request '{request.GetType().Name}' is not an IRequest<{typeof(TResponse).Name}>. Map it with a MediatR request type that returns the matching response.");
        }

        return new ValueTask<TResponse>(sender.Send(typedRequest, cancellationToken));
    }

    public ValueTask DispatchAsync(object request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // A no-response MediatR request implements IRequest (i.e. IRequest<Unit>). The object-typed
        // Send overload dispatches it; the Unit result is discarded.
        return new ValueTask(sender.Send(request, cancellationToken));
    }
}
