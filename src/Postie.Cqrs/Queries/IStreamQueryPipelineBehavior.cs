namespace Postie.Cqrs.Queries;

/// <summary>
/// A behavior that wraps the handling of a streaming query. Behaviors run in registration order, each
/// surrounding the next and ultimately the handler.
/// </summary>
/// <typeparam name="TQuery">The type of the query.</typeparam>
/// <typeparam name="TResponse">The type of each streamed item.</typeparam>
public interface IStreamQueryPipelineBehavior<in TQuery, TResponse> where TQuery : IStreamQuery<TResponse>
{
    /// <summary>
    /// Handles the query, invoking <paramref name="next"/> to continue the pipeline.
    /// </summary>
    /// <param name="query">The query being dispatched.</param>
    /// <param name="next">The continuation. Call it to run the rest of the pipeline; skip it to short-circuit.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while enumerating.</param>
    /// <returns>The stream, either from <paramref name="next"/> or produced by this behavior.</returns>
    IAsyncEnumerable<TResponse> Handle(TQuery query, StreamHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}
