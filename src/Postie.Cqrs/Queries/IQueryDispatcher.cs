namespace Postie.Cqrs.Queries;

/// <summary>
/// Dispatches queries.
/// </summary>
public interface IQueryDispatcher
{
    /// <summary>
    /// Dispatches a query to a query handler.
    /// </summary>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <param name="query">The query to dispatch.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>The response from the query.</returns>
    ValueTask<TResponse> Dispatch<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default);
}
