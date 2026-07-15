using Postie.Cqrs.Queries;

namespace Postie.Cqrs.Tests.Queries;

internal class TestQueryHandler : IQueryHandler<TestQuery, string>
{
    public ValueTask<string> Handle(TestQuery query, CancellationToken cancellationToken) =>
        new(query.Input.ToUpperInvariant());
}
