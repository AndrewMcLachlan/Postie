namespace Postie.Cqrs.Commands;

/// <summary>
/// Dispatches commands.
/// </summary>
public interface ICommandDispatcher
{
    /// <summary>
    /// Dispatches a command to a command handler and returns a response.
    /// </summary>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <param name="command">The command to dispatch.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>The response from the command.</returns>
    ValueTask<TResponse> Dispatch<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a command that does not return a response.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>A task.</returns>
    ValueTask Execute(ICommand command, CancellationToken cancellationToken = default);
}
