using Microsoft.Extensions.DependencyInjection;
using Postie.Cqrs.Queries;

namespace Postie.Cqrs.Tests.Queries;

public class QueryTests
{
    /// <summary>
    /// Given a query dispatcher with registered query handlers.
    /// When a TestQuery with input "Abc" is dispatched.
    /// Then the handler returns the uppercased result "ABC".
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task DispatchGenericQueryReturnsUppercasedResult()
    {
        ServiceCollection services = new();
        services.AddQueryHandlers(GetType().Assembly);
        var queryDispatcher = services.BuildServiceProvider().GetRequiredService<IQueryDispatcher>();

        var result = await queryDispatcher.Dispatch(new TestQuery { Input = "Abc" }, TestContext.Current.CancellationToken);

        Assert.Equal("ABC", result);
    }
}
