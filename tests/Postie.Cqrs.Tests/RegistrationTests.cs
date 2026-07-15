using Microsoft.Extensions.DependencyInjection;
using Postie.Cqrs.Commands;
using Postie.Cqrs.Queries;

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
}
