using Postie.AspNetCore;
using Postie.Cqrs.Queries;

namespace Postie.Cqrs.AspNetCore;

/// <summary>
/// An <see cref="IStreamEndpointDispatcher"/> that routes streaming endpoint requests to Postie's own
/// stream query dispatcher.
/// </summary>
/// <param name="streamQueryDispatcher">The stream query dispatcher.</param>
internal sealed class PostieStreamEndpointDispatcher(IStreamQueryDispatcher streamQueryDispatcher) : IStreamEndpointDispatcher
{
    public IAsyncEnumerable<TResponse> DispatchStream<TResponse>(object request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request is IStreamQuery<TResponse> streamQuery
            ? streamQueryDispatcher.Dispatch(streamQuery, cancellationToken)
            : throw new InvalidOperationException($"Request '{request.GetType().Name}' is not an IStreamQuery<{typeof(TResponse).Name}>. Map it with a stream query type that implements the matching Postie interface.");
    }
}
