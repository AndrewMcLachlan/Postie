using Microsoft.Extensions.DependencyInjection;
using Postie.AspNetCore;
using Postie.Cqrs.Queries;

namespace Postie.Cqrs.AspNetCore.Tests;

public class AddPostieRegistrationTests
{
    /// <summary>
    /// Given no assemblies passed to AddPostie.
    /// When registration runs.
    /// Then an ArgumentException directs the caller to pass an assembly or use the generic overload.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void AddPostieWithNoAssembliesThrows()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentException>(() => services.AddPostie());

        Assert.Equal("assemblies", exception.ParamName);
    }

    /// <summary>
    /// Given a marker type from the handlers assembly.
    /// When AddPostie is called with the generic marker overload.
    /// Then handlers and the endpoint dispatcher are registered.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void AddPostieWithMarkerRegistersHandlersAndDispatcher()
    {
        var services = new ServiceCollection();

        services.AddPostie<GetGreeting>();
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IQueryHandler<GetGreeting, string>>());
        Assert.NotNull(provider.GetService<IEndpointDispatcher>());
    }
}
