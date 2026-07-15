using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Postie.AspNetCore;

namespace Postie.Cqrs.AspNetCore.Tests;

public class EndpointMappingTests
{
    private static async Task<HttpClient> StartAsync(Action<IEndpointRouteBuilder> map)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddPostie(typeof(GetGreeting).Assembly);

        var app = builder.Build();
        map(app);
        await app.StartAsync();
        return app.GetTestClient();
    }

    /// <summary>
    /// Given a query mapped with MapQuery and bound from the route.
    /// When the GET endpoint is called.
    /// Then the query handler runs and its response is returned with 200 OK.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MapQueryDispatchesQueryAndReturnsOk()
    {
        var client = await StartAsync(app => app.MapQuery<GetGreeting, string>("/greeting/{name}"));

        var response = await client.GetAsync("/greeting/World", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Hello, World", await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Given a create command mapped with MapPostCreate and bound from the body.
    /// When the POST endpoint is called.
    /// Then the command handler runs and 201 Created with a Location header is returned.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MapPostCreateDispatchesCommandAndReturnsCreated()
    {
        var client = await StartAsync(app =>
        {
            app.MapQuery<GetGreeting, string>("/widgets/{name}").WithName("GetWidget");
            app.MapPostCreate<CreateWidget, Widget>("/widgets", "GetWidget", w => new { name = w.Name });
        });

        var response = await client.PostAsJsonAsync("/widgets", new CreateWidget("Sprocket"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var widget = await response.Content.ReadFromJsonAsync<Widget>(TestContext.Current.CancellationToken);
        Assert.Equal(new Widget(42, "Sprocket"), widget);
    }

    /// <summary>
    /// Given a hybrid command whose id binds from the route and payload from the body.
    /// When the PUT endpoint is called with Parameters binding.
    /// Then both sources are bound and the response reflects them.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MapPutCommandBindsHybridRouteAndBody()
    {
        var client = await StartAsync(app =>
            app.MapPutCommand<RenameWidget, Widget>("/widgets/{id}", binding: RequestBinding.Parameters));

        var response = await client.PutAsJsonAsync("/widgets/7", new RenameWidgetBody("Renamed"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var widget = await response.Content.ReadFromJsonAsync<Widget>(TestContext.Current.CancellationToken);
        Assert.Equal(new Widget(7, "Renamed"), widget);
    }

    /// <summary>
    /// Given a no-response command mapped with MapDeleteCommand.
    /// When the DELETE endpoint is called.
    /// Then the command is executed and 204 No Content is returned.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MapDeleteCommandDispatchesCommandAndReturnsNoContent()
    {
        var client = await StartAsync(app => app.MapDeleteCommand<DeleteWidget>("/widgets/{id}"));

        var response = await client.DeleteAsync("/widgets/7", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
