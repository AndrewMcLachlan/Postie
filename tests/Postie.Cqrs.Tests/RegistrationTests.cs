using Microsoft.Extensions.DependencyInjection;
using Postie.Cqrs.Commands;
using Postie.Cqrs.Queries;
using Postie.Cqrs.Tests.Commands;
using Postie.Cqrs.Tests.Queries;

namespace Postie.Cqrs.Tests;

public class RegistrationTests
{
    private record Ping : IQuery<string>;

    // These two conflicting handlers are abstract so the assembly scanner (which skips abstract types)
    // does not discover them in the other tests that scan this whole assembly. The duplicate guard
    // fires on registration before any instantiation, so abstractness is irrelevant to what is tested.
    private abstract class PingHandlerOne : IQueryHandler<Ping, string>
    {
        public ValueTask<string> Handle(Ping query, CancellationToken cancellationToken) => new("one");
    }

    private abstract class PingHandlerTwo : IQueryHandler<Ping, string>
    {
        public ValueTask<string> Handle(Ping query, CancellationToken cancellationToken) => new("two");
    }

    /// <summary>
    /// Given a query handler already registered for a request type.
    /// When a second, different handler is registered for the same request type.
    /// Then an InvalidOperationException naming both handlers is thrown.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void RegisteringTwoHandlersForOneRequestThrows()
    {
        ServiceCollection services = new();
        services.AddQueryHandler<PingHandlerOne, Ping, string>();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddQueryHandler<PingHandlerTwo, Ping, string>());

        Assert.Contains(nameof(PingHandlerOne), exception.Message);
        Assert.Contains(nameof(PingHandlerTwo), exception.Message);
    }

    /// <summary>
    /// Given an assembly containing command and query handlers.
    /// When AddCqrs is called with that assembly.
    /// Then both dispatchers are resolvable.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void AddCqrsRegistersBothDispatchers()
    {
        ServiceCollection services = new();
        services.AddCqrs(typeof(RegistrationTests).Assembly);

        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ICommandDispatcher>());
        Assert.NotNull(provider.GetRequiredService<IQueryDispatcher>());
    }

    /// <summary>
    /// Given handlers registered individually via the explicit generic overloads.
    /// When the matching requests are dispatched.
    /// Then each handler runs and returns its response.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExplicitHandlerRegistrationsDispatch()
    {
        ServiceCollection services = new();
        services.AddCommandHandler<TestCommandHandler, TestCommand, bool>();
        services.AddCommandHandler<NoOpCommandHandler, NoOpCommand>();
        services.AddQueryHandler<TestQueryHandler, TestQuery, string>();
        services.AddStreamQueryHandler<StreamQueryTests.CountdownHandler, StreamQueryTests.Countdown, int>();
        var provider = services.BuildServiceProvider();

        var commandResult = await provider.GetRequiredService<ICommandDispatcher>()
            .Dispatch(new TestCommand { Input = "ABC" }, TestContext.Current.CancellationToken);
        var queryResult = await provider.GetRequiredService<IQueryDispatcher>()
            .Dispatch(new TestQuery { Input = "abc" }, TestContext.Current.CancellationToken);

        var streamed = new List<int>();
        await foreach (var item in provider.GetRequiredService<IStreamQueryDispatcher>()
            .Dispatch(new StreamQueryTests.Countdown(2), TestContext.Current.CancellationToken))
        {
            streamed.Add(item);
        }

        await provider.GetRequiredService<ICommandDispatcher>()
            .Execute(new NoOpCommand(), TestContext.Current.CancellationToken);

        Assert.True(commandResult);
        Assert.Equal("ABC", queryResult);
        Assert.Equal([2, 1], streamed);
    }
}
