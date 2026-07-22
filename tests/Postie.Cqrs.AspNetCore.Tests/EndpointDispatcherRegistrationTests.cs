using Microsoft.Extensions.DependencyInjection;
using Postie.AspNetCore;

namespace Postie.Cqrs.AspNetCore.Tests;

public class EndpointDispatcherRegistrationTests
{
    /// <summary>
    /// Given handlers registered separately with AddQueryHandlers and AddCommandHandlers (no stream
    /// handlers) and AddPostieEndpointDispatcher.
    /// When IEndpointDispatcher is resolved.
    /// Then resolution succeeds and a query dispatches through it end-to-end.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task QueryAndCommandOnlyRegistrationResolvesEndpointDispatcher()
    {
        var services = new ServiceCollection();
        var assembly = typeof(GetGreeting).Assembly;

        services.AddQueryHandlers(assembly);
        services.AddCommandHandlers(assembly);
        services.AddPostieEndpointDispatcher();

        using var provider = services.BuildServiceProvider();

        var dispatcher = provider.GetRequiredService<IEndpointDispatcher>();

        var response = await dispatcher.DispatchAsync<string>(new GetGreeting("World"), CancellationToken.None);

        Assert.Equal("Hello, World", response);
    }

    /// <summary>
    /// Given handlers registered separately with AddQueryHandlers and AddCommandHandlers (no stream
    /// handlers) and AddPostieEndpointDispatcher.
    /// When the provider is built with ValidateOnBuild (the Development-environment default).
    /// Then validation passes — an app that never maps a stream endpoint must not pay a startup
    /// failure for stream support it never registered.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task QueryAndCommandOnlyRegistrationPassesBuildValidation()
    {
        var services = new ServiceCollection();
        var assembly = typeof(GetGreeting).Assembly;

        services.AddQueryHandlers(assembly);
        services.AddCommandHandlers(assembly);
        services.AddPostieEndpointDispatcher();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        var dispatcher = provider.GetRequiredService<IEndpointDispatcher>();

        var response = await dispatcher.DispatchAsync<string>(new GetGreeting("World"), CancellationToken.None);

        Assert.Equal("Hello, World", response);
    }

    /// <summary>
    /// Given handlers registered separately with AddQueryHandlers and AddCommandHandlers (no stream
    /// dispatcher registered) and AddPostieEndpointDispatcher.
    /// When IStreamEndpointDispatcher is resolved.
    /// Then resolution fails with a focused diagnostic naming the missing IStreamQueryDispatcher.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void QueryAndCommandOnlyRegistrationFailsToResolveStreamEndpointDispatcher()
    {
        var services = new ServiceCollection();
        var assembly = typeof(GetGreeting).Assembly;

        services.AddQueryHandlers(assembly);
        services.AddCommandHandlers(assembly);
        services.AddPostieEndpointDispatcher();

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IStreamEndpointDispatcher>());

        Assert.Contains("IStreamQueryDispatcher", exception.Message);
    }

    /// <summary>
    /// Given handlers registered with AddCqrs (including stream handlers) and AddPostieEndpointDispatcher.
    /// When IStreamEndpointDispatcher is resolved.
    /// Then resolution succeeds and a stream query dispatches through it end-to-end.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task FullRegistrationResolvesStreamEndpointDispatcher()
    {
        var services = new ServiceCollection();
        var assembly = typeof(GetGreeting).Assembly;

        services.AddCqrs(assembly);
        services.AddPostieEndpointDispatcher();

        using var provider = services.BuildServiceProvider();

        var dispatcher = provider.GetRequiredService<IStreamEndpointDispatcher>();

        var widgets = new List<Widget>();

        await foreach (var widget in dispatcher.DispatchStream<Widget>(new StreamWidgets(3), CancellationToken.None))
        {
            widgets.Add(widget);
        }

        Assert.Equal(3, widgets.Count);
    }
}
