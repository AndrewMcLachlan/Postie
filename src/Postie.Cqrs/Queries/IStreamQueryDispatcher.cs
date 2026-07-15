namespace Postie.Cqrs.Queries;

/// <summary>
/// Dispatches streaming queries.
/// </summary>
public interface IStreamQueryDispatcher
{
    /// <summary>
    /// Dispatches a streaming query to its handler.
    /// </summary>
    /// <typeparam name="TResponse">The type of each streamed item.</typeparam>
    /// <param name="query">The query to dispatch.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while enumerating.</param>
    /// <returns>The stream of responses.</returns>
    IAsyncEnumerable<TResponse> Dispatch<TResponse>(IStreamQuery<TResponse> query, CancellationToken cancellationToken = default);
}
