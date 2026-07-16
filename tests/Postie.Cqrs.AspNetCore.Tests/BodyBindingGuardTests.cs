using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Postie.AspNetCore;

namespace Postie.Cqrs.AspNetCore.Tests;

public class BodyBindingGuardTests
{
    private static WebApplication BuildApp()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddPostie(typeof(GetGreeting).Assembly);
        return builder.Build();
    }

    /// <summary>
    /// Given a command with [FromRoute] and [FromBody] members.
    /// When it is mapped with the default Body binding.
    /// Then mapping throws at startup naming the command and the fix, instead of silently ignoring the attributes.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void BodyBindingWithBindingSourceAttributesThrowsAtMapTime()
    {
        var app = BuildApp();

        var exception = Assert.Throws<InvalidOperationException>(() => app.MapPutCommand<RenameWidget, Widget>("/widgets/{id}"));

        Assert.Contains(nameof(RenameWidget), exception.Message);
        Assert.Contains("Id", exception.Message);
        Assert.Contains(nameof(RequestBinding.Parameters), exception.Message);
    }

    /// <summary>
    /// Given the same hybrid command.
    /// When it is mapped with Parameters binding.
    /// Then mapping succeeds.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ParametersBindingWithBindingSourceAttributesMapsCleanly()
    {
        var app = BuildApp();

        app.MapPutCommand<RenameWidget, Widget>("/widgets/{id}", binding: RequestBinding.Parameters);
    }

    /// <summary>
    /// Given a command with no binding-source attributes.
    /// When it is mapped with Body binding.
    /// Then mapping succeeds.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void BodyBindingWithoutBindingSourceAttributesMapsCleanly()
    {
        var app = BuildApp();

        app.MapCommand<SubmitWidget, Widget>("/widgets");
    }

    /// <summary>
    /// Given a hybrid command with binding-source attributes.
    /// When it is mapped with MapPostCreate (Body binding by default).
    /// Then mapping throws at startup, proving the guard covers the create path.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void BodyBindingGuardCoversCreatePath()
    {
        var app = BuildApp();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            app.MapPostCreate<RenameWidget, Widget>("/widgets", "GetWidget", w => new { id = w.Id }));

        Assert.Contains(nameof(RenameWidget), exception.Message);
    }

    /// <summary>
    /// Given a no-response hybrid command with binding-source attributes.
    /// When it is mapped with MapCommand (Body binding by default).
    /// Then mapping throws at startup, proving the guard covers the void command path.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void BodyBindingGuardCoversVoidCommandPath()
    {
        var app = BuildApp();

        var exception = Assert.Throws<InvalidOperationException>(() => app.MapCommand<ArchiveWidget>("/widgets/archive"));

        Assert.Contains(nameof(ArchiveWidget), exception.Message);
    }
}
