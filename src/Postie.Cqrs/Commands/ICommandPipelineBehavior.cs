namespace Postie.Cqrs.Commands;

/// <summary>
/// A behavior that wraps the handling of a command that returns a response, for cross-cutting concerns
/// such as logging, validation or transactions. Behaviors run in registration order, each surrounding
/// the next and ultimately the handler.
/// </summary>
/// <typeparam name="TCommand">The type of the command.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public interface ICommandPipelineBehavior<in TCommand, TResponse> where TCommand : ICommand<TResponse>
{
    /// <summary>
    /// Handles the command, invoking <paramref name="next"/> to continue the pipeline.
    /// </summary>
    /// <param name="command">The command being dispatched.</param>
    /// <param name="next">The continuation. Call it to run the rest of the pipeline; skip it to short-circuit.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>The response, either from <paramref name="next"/> or produced by this behavior.</returns>
    ValueTask<TResponse> Handle(TCommand command, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}

/// <summary>
/// A behavior that wraps the handling of a command that returns no response. Behaviors run in
/// registration order, each surrounding the next and ultimately the handler.
/// </summary>
/// <typeparam name="TCommand">The type of the command.</typeparam>
public interface ICommandPipelineBehavior<in TCommand> where TCommand : ICommand
{
    /// <summary>
    /// Handles the command, invoking <paramref name="next"/> to continue the pipeline.
    /// </summary>
    /// <param name="command">The command being dispatched.</param>
    /// <param name="next">The continuation. Call it to run the rest of the pipeline; skip it to short-circuit.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the rest of the pipeline.</returns>
    ValueTask Handle(TCommand command, RequestHandlerDelegate next, CancellationToken cancellationToken);
}
