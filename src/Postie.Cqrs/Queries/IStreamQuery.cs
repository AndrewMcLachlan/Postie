namespace Postie.Cqrs.Queries;

/// <summary>
/// Represents a query that returns a stream of responses.
/// </summary>
/// <typeparam name="TResponse">The type of each streamed item.</typeparam>
public interface IStreamQuery<TResponse> : IDispatchable
{
}
