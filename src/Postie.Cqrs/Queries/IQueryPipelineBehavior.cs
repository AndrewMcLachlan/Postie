namespace Postie.Cqrs.Queries;

/// <summary>
/// A behavior that wraps the handling of a query, for cross-cutting concerns such as logging,
/// validation, caching or timing. Behaviors run in registration order, each surrounding the next and
/// ultimately the handler.
/// </summary>
/// <typeparam name="TQuery">The type of the query.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public interface IQueryPipelineBehavior<in TQuery, TResponse> where TQuery : IQuery<TResponse>
{
    /// <summary>
    /// Handles the query, invoking <paramref name="next"/> to continue the pipeline.
    /// </summary>
    /// <param name="query">The query being dispatched.</param>
    /// <param name="next">The continuation. Call it to run the rest of the pipeline; skip it to short-circuit.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>The response, either from <paramref name="next"/> or produced by this behavior.</returns>
    ValueTask<TResponse> Handle(TQuery query, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}
