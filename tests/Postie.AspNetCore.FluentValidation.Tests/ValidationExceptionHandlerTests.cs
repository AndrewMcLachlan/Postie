using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Postie.AspNetCore;

namespace Postie.AspNetCore.FluentValidation.Tests;

public class ValidationExceptionHandlerTests
{
    private static async Task<HttpClient> StartAsync(Action<IEndpointRouteBuilder> map)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddPostie<CreateWidget>();
        builder.Services.AddPostieValidation<CreateWidgetValidator>();
        builder.Services.AddPostieValidationExceptionHandler();

        var app = builder.Build();
        app.UseExceptionHandler();
        map(app);
        await app.StartAsync();
        return app.GetTestClient();
    }

    /// <summary>
    /// Given a command with a failing validator and the Postie validation exception handler.
    /// When the endpoint is called with an invalid payload.
    /// Then 400 is returned as validation problem details with the errors grouped by property.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task InvalidCommandReturnsValidationProblemDetails()
    {
        var client = await StartAsync(app => app.MapCommand<CreateWidget, Widget>("/widgets"));

        var response = await client.PostAsJsonAsync("/widgets", new CreateWidget(""), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey(nameof(CreateWidget.Name)));
        Assert.NotEmpty(problem.Errors[nameof(CreateWidget.Name)]);
    }

    /// <summary>
    /// Given a command with a passing validator.
    /// When the endpoint is called with a valid payload.
    /// Then the pipeline proceeds and 200 OK is returned.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ValidCommandPassesThrough()
    {
        var client = await StartAsync(app => app.MapCommand<CreateWidget, Widget>("/widgets"));

        var response = await client.PostAsJsonAsync("/widgets", new CreateWidget("Sprocket"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new Widget(42, "Sprocket"), await response.Content.ReadFromJsonAsync<Widget>(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Given a handler that throws a non-validation exception.
    /// When the endpoint is called.
    /// Then the handler does not swallow it and the response is a 500.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task NonValidationExceptionIsNotHandled()
    {
        var client = await StartAsync(app => app.MapCommand<ExplodeWidget, Widget>("/widgets/explode"));

        var response = await client.PostAsJsonAsync("/widgets/explode", new ExplodeWidget("x"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}
