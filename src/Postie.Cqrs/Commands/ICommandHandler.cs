namespace Postie.Cqrs.Commands;

/// <summary>
/// A handler for a command that returns a response.
/// </summary>
/// <typeparam name="TCommand">The type of the command.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public interface ICommandHandler<TCommand, TResponse> where TCommand : ICommand<TResponse>
{
    /// <summary>
    /// Handles the command.
    /// </summary>
    /// <param name="command">The command instance to handle.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>The command response.</returns>
    ValueTask<TResponse> Handle(TCommand command, CancellationToken cancellationToken);
}

/// <summary>
/// A handler for a command that does not return a response.
/// </summary>
/// <typeparam name="TCommand">The type of the command.</typeparam>
public interface ICommandHandler<TCommand> where TCommand : ICommand
{
    /// <summary>
    /// Handles the command.
    /// </summary>
    /// <param name="command">The command instance to handle.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>A task.</returns>
    ValueTask Handle(TCommand command, CancellationToken cancellationToken);
}
