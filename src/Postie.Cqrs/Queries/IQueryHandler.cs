namespace Postie.Cqrs.Queries;

/// <summary>
/// A handler for a query.
/// </summary>
/// <typeparam name="TQuery">The type of the query.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public interface IQueryHandler<TQuery, TResponse> where TQuery : IQuery<TResponse>
{
    /// <summary>
    /// Handles the query.
    /// </summary>
    /// <param name="query">The query to handle.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>The query response.</returns>
    ValueTask<TResponse> Handle(TQuery query, CancellationToken cancellationToken);
}
