using Postie.Cqrs.Commands;

namespace Postie.Cqrs.Tests.Commands;

internal class TestCommandHandler : ICommandHandler<TestCommand, bool>
{
    public ValueTask<bool> Handle(TestCommand command, CancellationToken cancellationToken) =>
        new(command.Input.Equals(command.Input.ToUpperInvariant()));
}
