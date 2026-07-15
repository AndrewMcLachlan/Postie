namespace Postie.AspNetCore;

/// <summary>
/// An optional companion to <see cref="IEndpointDispatcher"/> that dispatches streaming requests. Only
/// needed when the app maps streaming endpoints with <c>MapStreamQuery</c>.
/// </summary>
/// <remarks>
/// The Postie and MediatR adapters register this alongside <see cref="IEndpointDispatcher"/>. A
/// roll-your-own mediator only needs to implement this if it maps streams.
/// </remarks>
public interface IStreamEndpointDispatcher
{
    /// <summary>
    /// Dispatches a streaming request.
    /// </summary>
    /// <typeparam name="TResponse">The type of each streamed item.</typeparam>
    /// <param name="request">The request instance bound from the HTTP request.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while enumerating.</param>
    /// <returns>The stream of responses.</returns>
    IAsyncEnumerable<TResponse> DispatchStream<TResponse>(object request, CancellationToken cancellationToken);
}
