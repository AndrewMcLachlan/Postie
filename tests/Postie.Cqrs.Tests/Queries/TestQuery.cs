using Postie.Cqrs.Queries;

namespace Postie.Cqrs.Tests.Queries;

internal class TestQuery : IQuery<string>
{
    public string Input { get; init; }
}
