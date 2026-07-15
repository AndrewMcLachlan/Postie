namespace Postie.Cqrs;

/// <summary>
/// An object that can be dispatched.
/// </summary>
public interface IDispatchable
{
}

/// <summary>
/// An object that can be dispatched and returns a response.
/// </summary>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public interface IDispatchable<TResponse> : IDispatchable
{
}
