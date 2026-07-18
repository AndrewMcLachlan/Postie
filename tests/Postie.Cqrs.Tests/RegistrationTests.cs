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

    /// <summary>
    /// Given no assemblies passed to AddCqrs.
    /// When registration runs.
    /// Then an ArgumentException directs the caller to pass an assembly or use the generic overload,
    /// instead of silently scanning the calling assembly.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void AddCqrsWithNoAssembliesThrows()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentException>(() => services.AddCqrs());

        Assert.Equal("assemblies", exception.ParamName);
    }

    /// <summary>
    /// Given a marker type from the handlers assembly.
    /// When AddCqrs is called with the generic marker overload.
    /// Then handlers from the marker's assembly are registered.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void AddCqrsWithMarkerRegistersHandlersFromMarkerAssembly()
    {
        var services = new ServiceCollection();

        services.AddCqrs<AddCqrsMarker>();

        Assert.Contains(services, s => s.ServiceType.IsGenericType && s.ServiceType.GetGenericTypeDefinition() == typeof(Postie.Cqrs.Queries.IQueryHandler<,>));
        Assert.Contains(services, s => s.ServiceType == typeof(Postie.Cqrs.Queries.IQueryDispatcher));
    }

    private sealed class AddCqrsMarker;

    /// <summary>
    /// Given an open generic that does not implement IQueryPipelineBehavior&lt;,&gt;.
    /// When AddQueryPipelineBehavior is called with it.
    /// Then an ArgumentException naming "behaviorType" is thrown at registration time, instead of MS.DI
    /// failing only when the behavior is resolved.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void AddQueryPipelineBehaviorWithNonImplementingOpenGenericThrows()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentException>(() => services.AddQueryPipelineBehavior(typeof(List<>)));

        Assert.Equal("behaviorType", exception.ParamName);
    }

    /// <summary>
    /// Given an open generic that does not implement IStreamQueryPipelineBehavior&lt;,&gt;.
    /// When AddStreamQueryPipelineBehavior is called with it.
    /// Then an ArgumentException naming "behaviorType" is thrown at registration time.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void AddStreamQueryPipelineBehaviorWithNonImplementingOpenGenericThrows()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentException>(() => services.AddStreamQueryPipelineBehavior(typeof(List<>)));

        Assert.Equal("behaviorType", exception.ParamName);
    }

    /// <summary>
    /// Given a one-type-parameter open generic that does not implement ICommandPipelineBehavior&lt;&gt;.
    /// When AddCommandPipelineBehavior is called with it.
    /// Then an ArgumentException naming "behaviorType" is thrown at registration time (the arity-based
    /// branch picks the one-parameter interface first, and validates against that one).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void AddCommandPipelineBehaviorWithNonImplementingOneArgOpenGenericThrows()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentException>(() => services.AddCommandPipelineBehavior(typeof(List<>)));

        Assert.Equal("behaviorType", exception.ParamName);
    }

    /// <summary>
    /// Given a two-type-parameter open generic that does not implement ICommandPipelineBehavior&lt;,&gt;.
    /// When AddCommandPipelineBehavior is called with it.
    /// Then an ArgumentException naming "behaviorType" is thrown at registration time (the arity-based
    /// branch picks the two-parameter interface first, and validates against that one).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void AddCommandPipelineBehaviorWithNonImplementingTwoArgOpenGenericThrows()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentException>(() => services.AddCommandPipelineBehavior(typeof(Dictionary<,>)));

        Assert.Equal("behaviorType", exception.ParamName);
    }
}
