using Postie.Cqrs.Commands;

namespace Postie.Cqrs.Tests.Commands;

internal class ThrowingCommand : ICommand
{
}

internal class ThrowingCommandHandler : ICommandHandler<ThrowingCommand>
{
    // Throws synchronously (the method is not async) so the throw propagates through the compiled
    // invoker as its own exception type rather than a wrapped TargetInvocationException.
    public ValueTask Handle(ThrowingCommand command, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("boom");
}
