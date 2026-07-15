namespace Postie.Cqrs.Queries;

/// <summary>
/// A handler for a streaming query.
/// </summary>
/// <typeparam name="TQuery">The type of the query.</typeparam>
/// <typeparam name="TResponse">The type of each streamed item.</typeparam>
public interface IStreamQueryHandler<TQuery, TResponse> where TQuery : IStreamQuery<TResponse>
{
    /// <summary>
    /// Handles the query, producing a stream of responses.
    /// </summary>
    /// <param name="query">The query to handle.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while enumerating.</param>
    /// <returns>The stream of responses.</returns>
    IAsyncEnumerable<TResponse> Handle(TQuery query, CancellationToken cancellationToken);
}
