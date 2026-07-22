using Microsoft.Extensions.DependencyInjection;
using Postie.AspNetCore;

namespace Postie.Cqrs.AspNetCore.Tests;

public class EndpointDispatcherGuardTests
{
    private static ServiceProvider BuildProvider() =>
        new ServiceCollection().AddPostie(typeof(GetGreeting).Assembly).BuildServiceProvider();

    /// <summary>
    /// Given a request dispatched for a response type it neither queries nor commands.
    /// When it goes through the Postie endpoint dispatcher.
    /// Then an InvalidOperationException names the request, both expected interfaces and the fix.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ResponseDispatchRejectsRequestWithWrongShape()
    {
        await using var provider = BuildProvider();
        var dispatcher = provider.GetRequiredService<IEndpointDispatcher>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await dispatcher.DispatchAsync<string>(new FindWidget(1), TestContext.Current.CancellationToken));

        Assert.Contains(nameof(FindWidget), exception.Message);
        Assert.Contains("IQuery<String>", exception.Message);
        Assert.Contains("ICommand<String>", exception.Message);
    }

    /// <summary>
    /// Given a request that is not an ICommand.
    /// When it is dispatched through the no-response path of the Postie endpoint dispatcher.
    /// Then an InvalidOperationException names the request and the required interface.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task VoidDispatchRejectsNonCommand()
    {
        await using var provider = BuildProvider();
        var dispatcher = provider.GetRequiredService<IEndpointDispatcher>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await dispatcher.DispatchAsync(new Widget(1, "x"), TestContext.Current.CancellationToken));

        Assert.Contains(nameof(Widget), exception.Message);
        Assert.Contains("ICommand", exception.Message);
    }

    /// <summary>
    /// Given a request that is not an IStreamQuery for the item type.
    /// When it is dispatched through the Postie stream endpoint dispatcher.
    /// Then an InvalidOperationException is thrown at dispatch, before any enumeration.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task StreamDispatchRejectsNonStreamQuery()
    {
        await using var provider = BuildProvider();
        var dispatcher = provider.GetRequiredService<IStreamEndpointDispatcher>();

        var exception = Assert.Throws<InvalidOperationException>(
            () => dispatcher.DispatchStream<string>(new FindWidget(1), TestContext.Current.CancellationToken));

        Assert.Contains(nameof(FindWidget), exception.Message);
        Assert.Contains("IStreamQuery<String>", exception.Message);
    }
}
