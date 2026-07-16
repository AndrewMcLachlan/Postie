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
    /// Given a command mapped with MapCommand (POST returning a response).
    /// When the POST endpoint is called with a JSON body.
    /// Then the command handler runs and its response is returned with 200 OK.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MapCommandDispatchesCommandAndReturnsOk()
    {
        var client = await StartAsync(app => app.MapCommand<SubmitWidget, Widget>("/widgets"));

        var response = await client.PostAsJsonAsync("/widgets", new SubmitWidget("Cog"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new Widget(99, "Cog"), await response.Content.ReadFromJsonAsync<Widget>(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Given a no-response command mapped with MapCommand (POST).
    /// When the POST endpoint is called.
    /// Then the command is executed and 204 No Content is returned.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MapCommandVoidReturnsNoContent()
    {
        var client = await StartAsync(app => app.MapCommand<DeleteWidget>("/widgets/remove"));

        var response = await client.PostAsJsonAsync("/widgets/remove", new DeleteWidget(5), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>
    /// Given a command mapped with MapPatchCommand.
    /// When the PATCH endpoint is called.
    /// Then the command handler runs and its response is returned with 200 OK.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MapPatchCommandDispatchesCommandAndReturnsOk()
    {
        var client = await StartAsync(app => app.MapPatchCommand<SubmitWidget, Widget>("/widgets"));

        var response = await client.PatchAsJsonAsync("/widgets", new SubmitWidget("Patched"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new Widget(99, "Patched"), await response.Content.ReadFromJsonAsync<Widget>(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Given a no-response command mapped with MapPutCommand.
    /// When the PUT endpoint is called.
    /// Then the command is executed and 204 No Content is returned.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MapPutCommandVoidReturnsNoContent()
    {
        var client = await StartAsync(app => app.MapPutCommand<DeleteWidget>("/widgets/deactivate"));

        var response = await client.PutAsJsonAsync("/widgets/deactivate", new DeleteWidget(5), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>
    /// Given a create command mapped with MapPutCreate.
    /// When the PUT endpoint is called.
    /// Then the command handler runs and 201 Created with a Location header is returned.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MapPutCreateDispatchesCommandAndReturnsCreated()
    {
        var client = await StartAsync(app =>
        {
            app.MapQuery<GetGreeting, string>("/widgets/{name}").WithName("GetPutWidget");
            app.MapPutCreate<SubmitWidget, Widget>("/widgets", "GetPutWidget", w => new { name = w.Name });
        });

        var response = await client.PutAsJsonAsync("/widgets", new SubmitWidget("Made"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal(new Widget(99, "Made"), await response.Content.ReadFromJsonAsync<Widget>(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Given a command mapped with MapDeleteCommand returning a response.
    /// When the DELETE endpoint is called.
    /// Then the command handler runs and its response is returned with 200 OK.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MapDeleteCommandWithResponseReturnsOk()
    {
        var client = await StartAsync(app => app.MapDeleteCommand<PurgeWidget, Widget>("/widgets/{id}"));

        var response = await client.DeleteAsync("/widgets/8", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new Widget(8, "purged"), await response.Content.ReadFromJsonAsync<Widget>(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Given a streaming query mapped with MapStreamQuery.
    /// When the GET endpoint is called.
    /// Then the handler's items are streamed back as a JSON array.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MapStreamQueryStreamsItems()
    {
        var client = await StartAsync(app => app.MapStreamQuery<StreamWidgets, Widget>("/widgets/stream/{count}"));

        var widgets = await client.GetFromJsonAsync<List<Widget>>("/widgets/stream/3", TestContext.Current.CancellationToken);

        Assert.Equal([new Widget(1, "Widget 1"), new Widget(2, "Widget 2"), new Widget(3, "Widget 3")], widgets);
    }

    /// <summary>
    /// Given a create command mapped with MapPutCreate using the request-aware route-values overload.
    /// When the PUT endpoint is called.
    /// Then the Location header is built from the request's values (the client-supplied identity).
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MapPutCreateBuildsLocationFromRequest()
    {
        var client = await StartAsync(app =>
        {
            app.MapQuery<GetGreeting, string>("/widgets/{name}").WithName("GetPutRequestWidget");
            app.MapPutCreate<RegisterWidget, Widget>("/widgets", "GetPutRequestWidget", (request, _) => new { name = request.Name });
        });

        var response = await client.PutAsJsonAsync("/widgets", new RegisterWidget("FromRequest"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith("/widgets/FromRequest", response.Headers.Location.ToString());
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

    /// <summary>
    /// Given a query mapped with MapQuery whose handler returns null.
    /// When the GET endpoint is called for a missing resource.
    /// Then 404 Not Found is returned instead of 200 with a null body.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MapQueryReturnsNotFoundWhenHandlerReturnsNull()
    {
        var client = await StartAsync(app => app.MapQuery<FindWidget, Widget>("/widgets/find/{id}"));

        var response = await client.GetAsync("/widgets/find/0", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Given a query mapped with MapQuery whose handler returns a value.
    /// When the GET endpoint is called for an existing resource.
    /// Then the value is returned with 200 OK.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MapQueryReturnsOkWhenHandlerReturnsValue()
    {
        var client = await StartAsync(app => app.MapQuery<FindWidget, Widget>("/widgets/find/{id}"));

        var response = await client.GetAsync("/widgets/find/3", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new Widget(3, "found"), await response.Content.ReadFromJsonAsync<Widget>(TestContext.Current.CancellationToken));
    }
}
