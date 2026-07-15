namespace Postie.Cqrs;

/// <summary>
/// The continuation in a streaming pipeline: invokes the next behavior, or the handler if this is the
/// last behavior.
/// </summary>
/// <typeparam name="TResponse">The type of each streamed item.</typeparam>
/// <returns>The stream from the rest of the pipeline.</returns>
public delegate IAsyncEnumerable<TResponse> StreamHandlerDelegate<TResponse>();
