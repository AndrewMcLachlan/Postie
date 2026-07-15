using Microsoft.Extensions.DependencyInjection;
using Postie.Cqrs.Commands;

namespace Postie.Cqrs.Tests.Commands;

public class CommandTests
{
    /// <summary>
    /// Given a command dispatcher with registered command handlers.
    /// When a TestCommand with mixed-case input "Abc" is dispatched.
    /// Then the handler returns false.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task DispatchCommandWithMixedCaseInputReturnsFalse()
    {
        ServiceCollection services = new();
        services.AddCommandHandlers(GetType().Assembly);
        var commandDispatcher = services.BuildServiceProvider().GetRequiredService<ICommandDispatcher>();

        var result = await commandDispatcher.Dispatch(new TestCommand { Input = "Abc" }, TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    /// <summary>
    /// Given a command dispatcher with registered command handlers.
    /// When a TestCommand with uppercase input "ABC" is dispatched.
    /// Then the handler returns true.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task DispatchCommandWithUppercaseInputReturnsTrue()
    {
        ServiceCollection services = new();
        services.AddCommandHandlers(GetType().Assembly);
        var commandDispatcher = services.BuildServiceProvider().GetRequiredService<ICommandDispatcher>();

        var result = await commandDispatcher.Dispatch(new TestCommand { Input = "ABC" }, TestContext.Current.CancellationToken);

        Assert.True(result);
    }

    /// <summary>
    /// Given a command whose handler throws synchronously.
    /// When the command is executed through the dispatcher.
    /// Then the handler's original InvalidOperationException surfaces, not a TargetInvocationException.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task SynchronousHandlerThrowSurfacesOriginalExceptionType()
    {
        ServiceCollection services = new();
        services.AddCommandHandlers(GetType().Assembly);
        var commandDispatcher = services.BuildServiceProvider().GetRequiredService<ICommandDispatcher>();

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await commandDispatcher.Execute(new ThrowingCommand(), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Given a response-returning command referenced through the non-generic ICommand interface.
    /// When it is executed via the void Execute method that cannot resolve a handler.
    /// Then an InvalidOperationException is thrown whose message points to Dispatch&lt;TResponse&gt;.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ResponseCommandDispatchedAsVoidCommandThrowsHelpfulError()
    {
        ServiceCollection services = new();
        services.AddCommandHandlers(GetType().Assembly);
        var commandDispatcher = services.BuildServiceProvider().GetRequiredService<ICommandDispatcher>();

        ICommand command = new TestCommand { Input = "ABC" };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await commandDispatcher.Execute(command, TestContext.Current.CancellationToken));

        Assert.Contains("Dispatch<TResponse>", exception.Message);
    }

    /// <summary>
    /// Given a null command.
    /// When it is dispatched.
    /// Then an ArgumentNullException is thrown.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task DispatchNullCommandThrowsArgumentNullException()
    {
        ServiceCollection services = new();
        services.AddCommandHandlers(GetType().Assembly);
        var commandDispatcher = services.BuildServiceProvider().GetRequiredService<ICommandDispatcher>();

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await commandDispatcher.Dispatch<bool>(null, TestContext.Current.CancellationToken));
    }
}
