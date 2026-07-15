using Postie.Cqrs.Commands;

namespace Postie.Cqrs.Tests.Commands;

internal class NoOpCommand : ICommand
{
}

internal class NoOpCommandHandler : ICommandHandler<NoOpCommand>
{
    public ValueTask Handle(NoOpCommand command, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
