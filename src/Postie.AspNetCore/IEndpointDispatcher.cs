namespace Postie.AspNetCore;

/// <summary>
/// The single abstraction the endpoint mapping dispatches through, decoupling the endpoints from any
/// particular mediator.
/// </summary>
/// <remarks>
/// The engine never references a mediator; it only knows how to bind a request from an HTTP request,
/// hand it to an <see cref="IEndpointDispatcher"/>, and shape the result. To plug a mediator in,
/// register an implementation of this interface:
/// <list type="bullet">
/// <item>Postie's own mediator: reference <c>Postie.Cqrs.AspNetCore</c> and call <c>AddPostie(...)</c>.</item>
/// <item>MediatR: reference <c>Postie.AspNetCore.MediatR</c>.</item>
/// <item>Anything else: implement this interface (two methods) and register it.</item>
/// </list>
/// The request is typed as <see cref="object"/> so that no mediator's request marker (Postie's
/// <c>IQuery</c>/<c>ICommand</c>, MediatR's <c>IRequest</c>) leaks into the engine. Implementations
/// receive the concrete request instance the framework bound and route it however that mediator expects.
/// </remarks>
public interface IEndpointDispatcher
{
    /// <summary>
    /// Dispatches a request that returns a response.
    /// </summary>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <param name="request">The request instance bound from the HTTP request.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>The response.</returns>
    ValueTask<TResponse> DispatchAsync<TResponse>(object request, CancellationToken cancellationToken);

    /// <summary>
    /// Dispatches a request that does not return a response.
    /// </summary>
    /// <param name="request">The request instance bound from the HTTP request.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>A task.</returns>
    ValueTask DispatchAsync(object request, CancellationToken cancellationToken);
}
