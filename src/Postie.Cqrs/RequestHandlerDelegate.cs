namespace Postie.Cqrs;

/// <summary>
/// The continuation in a pipeline: invokes the next behavior, or the handler if this is the last behavior.
/// </summary>
/// <typeparam name="TResponse">The type of the response.</typeparam>
/// <returns>The response from the rest of the pipeline.</returns>
public delegate ValueTask<TResponse> RequestHandlerDelegate<TResponse>();

/// <summary>
/// The continuation in a pipeline for a request that returns no response.
/// </summary>
/// <returns>A task representing the rest of the pipeline.</returns>
public delegate ValueTask RequestHandlerDelegate();
