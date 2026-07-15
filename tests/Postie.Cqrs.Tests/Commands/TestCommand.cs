using Postie.Cqrs.Commands;

namespace Postie.Cqrs.Tests.Commands;

internal class TestCommand : ICommand<bool>
{
    public string Input { get; init; }
}
