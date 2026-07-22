using Microsoft.Extensions.DependencyInjection;
using Postie.AspNetCore;

namespace Postie.AspNetCore.MediatR.Tests;

public class MediatRDispatcherGuardTests
{
    private static ServiceProvider BuildProvider() =>
        new ServiceCollection()
            .AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetGreeting).Assembly))
            .AddPostieMediatR()
            .BuildServiceProvider();

    /// <summary>
    /// Given a request that is not an IRequest for the response type.
    /// When it is dispatched through the MediatR endpoint dispatcher.
    /// Then an InvalidOperationException names the request, the expected interface and the fix.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ResponseDispatchRejectsNonMediatRRequest()
    {
        await using var provider = BuildProvider();
        var dispatcher = provider.GetRequiredService<IEndpointDispatcher>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await dispatcher.DispatchAsync<string>(new Widget(1, "x"), TestContext.Current.CancellationToken));

        Assert.Contains(nameof(Widget), exception.Message);
        Assert.Contains("IRequest<String>", exception.Message);
    }

    /// <summary>
    /// Given a request that is not an IStreamRequest for the item type.
    /// When it is dispatched through the MediatR stream endpoint dispatcher.
    /// Then an InvalidOperationException is thrown at dispatch, before any enumeration.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task StreamDispatchRejectsNonStreamRequest()
    {
        await using var provider = BuildProvider();
        var dispatcher = provider.GetRequiredService<IStreamEndpointDispatcher>();

        var exception = Assert.Throws<InvalidOperationException>(
            () => dispatcher.DispatchStream<string>(new Widget(1, "x"), TestContext.Current.CancellationToken));

        Assert.Contains(nameof(Widget), exception.Message);
        Assert.Contains("IStreamRequest<String>", exception.Message);
    }
}
