namespace Postie.Cqrs.Commands;

/// <summary>
/// Represents a command that returns a response.
/// </summary>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public interface ICommand<TResponse> : ICommand, IDispatchable<TResponse>
{
}

/// <summary>
/// Represents a command that does not return a response.
/// </summary>
public interface ICommand : IDispatchable
{
}
