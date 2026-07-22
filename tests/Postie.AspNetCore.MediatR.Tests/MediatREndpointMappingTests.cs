using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Postie.AspNetCore;

namespace Postie.AspNetCore.MediatR.Tests;

public class MediatREndpointMappingTests
{
    private static async Task<HttpClient> StartAsync(Action<IEndpointRouteBuilder> map)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetGreeting).Assembly));
        builder.Services.AddPostieMediatR();

        var app = builder.Build();
        map(app);
        await app.StartAsync();
        return app.GetTestClient();
    }

    /// <summary>
    /// Given a MediatR query mapped with MapQuery and bound from the route.
    /// When the GET endpoint is called.
    /// Then MediatR handles the request and its response is returned with 200 OK.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MapQueryDispatchesThroughMediatRAndReturnsOk()
    {
        var client = await StartAsync(app => app.MapQuery<GetGreeting, string>("/greeting/{name}"));

        var response = await client.GetAsync("/greeting/World", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Hello, World", await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Given a MediatR create request mapped with MapPostCreate and bound from the body.
    /// When the POST endpoint is called.
    /// Then MediatR handles it and 201 Created with a Location header is returned.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MapPostCreateDispatchesThroughMediatRAndReturnsCreated()
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
    /// Given a hybrid MediatR request whose id binds from the route and payload from the body.
    /// When the PUT endpoint is called with Parameters binding.
    /// Then both sources are bound and MediatR's response reflects them.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MapPutCommandBindsHybridRouteAndBodyThroughMediatR()
    {
        var client = await StartAsync(app =>
            app.MapPutCommand<RenameWidget, Widget>("/widgets/{id}", binding: RequestBinding.Parameters));

        var response = await client.PutAsJsonAsync("/widgets/7", new RenameWidgetBody("Renamed"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var widget = await response.Content.ReadFromJsonAsync<Widget>(TestContext.Current.CancellationToken);
        Assert.Equal(new Widget(7, "Renamed"), widget);
    }

    /// <summary>
    /// Given a MediatR stream request mapped with MapStreamQuery.
    /// When the GET endpoint is called.
    /// Then MediatR's CreateStream is used and the items come back as a JSON array.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MapStreamQueryStreamsThroughMediatR()
    {
        var client = await StartAsync(app => app.MapStreamQuery<StreamWidgets, Widget>("/widgets/stream/{count}"));

        var widgets = await client.GetFromJsonAsync<List<Widget>>("/widgets/stream/3", TestContext.Current.CancellationToken);

        Assert.Equal([new Widget(1, "Widget 1"), new Widget(2, "Widget 2"), new Widget(3, "Widget 3")], widgets);
    }

    /// <summary>
    /// Given a no-response MediatR request mapped with MapDeleteCommand.
    /// When the DELETE endpoint is called.
    /// Then MediatR handles it and 204 No Content is returned.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MapDeleteCommandDispatchesThroughMediatRAndReturnsNoContent()
    {
        var client = await StartAsync(app => app.MapDeleteCommand<DeleteWidget>("/widgets/{id}"));

        var response = await client.DeleteAsync("/widgets/7", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>
    /// Given a MediatR request mapped with MapQuery whose handler returns null.
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
    /// Given a MediatR request mapped with MapCommand (POST returning a response).
    /// When the POST endpoint is called with a JSON body.
    /// Then MediatR handles it and its response is returned with 200 OK.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MapCommandDispatchesThroughMediatRAndReturnsOk()
    {
        var client = await StartAsync(app => app.MapCommand<SubmitWidget, Widget>("/widgets"));

        var response = await client.PostAsJsonAsync("/widgets", new SubmitWidget("Cog"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new Widget(99, "Cog"), await response.Content.ReadFromJsonAsync<Widget>(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Given a no-response MediatR request mapped with MapCommand (POST).
    /// When the POST endpoint is called.
    /// Then MediatR handles it and 204 No Content is returned.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MapCommandVoidReturnsNoContentThroughMediatR()
    {
        var client = await StartAsync(app => app.MapCommand<DeleteWidget>("/widgets/remove"));

        var response = await client.PostAsJsonAsync("/widgets/remove", new DeleteWidget(5), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>
    /// Given a MediatR request mapped with MapPatchCommand.
    /// When the PATCH endpoint is called.
    /// Then MediatR handles it and its response is returned with 200 OK.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MapPatchCommandDispatchesThroughMediatRAndReturnsOk()
    {
        var client = await StartAsync(app => app.MapPatchCommand<SubmitWidget, Widget>("/widgets"));

        var response = await client.PatchAsJsonAsync("/widgets", new SubmitWidget("Patched"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new Widget(99, "Patched"), await response.Content.ReadFromJsonAsync<Widget>(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Given a no-response MediatR request mapped with MapPutCommand.
    /// When the PUT endpoint is called.
    /// Then MediatR handles it and 204 No Content is returned.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MapPutCommandVoidReturnsNoContentThroughMediatR()
    {
        var client = await StartAsync(app => app.MapPutCommand<DeleteWidget>("/widgets/deactivate"));

        var response = await client.PutAsJsonAsync("/widgets/deactivate", new DeleteWidget(5), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>
    /// Given a MediatR create request mapped with MapPutCreate.
    /// When the PUT endpoint is called.
    /// Then MediatR handles it and 201 Created with a Location header is returned.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MapPutCreateDispatchesThroughMediatRAndReturnsCreated()
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
    /// Given a MediatR request mapped with MapDeleteCommand returning a response.
    /// When the DELETE endpoint is called.
    /// Then MediatR handles it and its response is returned with 200 OK.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MapDeleteCommandWithResponseReturnsOkThroughMediatR()
    {
        var client = await StartAsync(app => app.MapDeleteCommand<PurgeWidget, Widget>("/widgets/{id}"));

        var response = await client.DeleteAsync("/widgets/8", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new Widget(8, "purged"), await response.Content.ReadFromJsonAsync<Widget>(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Given a MediatR request mapped with MapQuery and QueryMethod.Post.
    /// When the POST endpoint is called with the criteria as a JSON body.
    /// Then MediatR handles it and its response is returned with 200 OK.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MapQueryWithPostBindsBodyThroughMediatR()
    {
        var client = await StartAsync(app => app.MapQuery<SearchWidgets, Widget>("/widgets/search", QueryMethod.Post));

        var response = await client.PostAsJsonAsync("/widgets/search", new SearchWidgets("cog", 2), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new Widget(2, "cog"), await response.Content.ReadFromJsonAsync<Widget>(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Given a MediatR request mapped with MapQuery and QueryMethod.Query.
    /// When the endpoint is called with the HTTP QUERY method and a JSON body.
    /// Then MediatR handles it and its response is returned with 200 OK.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MapQueryWithQueryVerbBindsBodyThroughMediatR()
    {
        var client = await StartAsync(app => app.MapQuery<SearchWidgets, Widget>("/widgets/search", QueryMethod.Query));

        var request = new HttpRequestMessage(new HttpMethod("QUERY"), "/widgets/search")
        {
            Content = JsonContent.Create(new SearchWidgets("sprocket", 5)),
        };
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new Widget(5, "sprocket"), await response.Content.ReadFromJsonAsync<Widget>(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Given a MediatR request with a nullable value-type response whose handler returns null.
    /// When the GET endpoint is called for a missing resource.
    /// Then 404 Not Found is returned.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MapQueryReturnsNotFoundForNullableValueTypeThroughMediatR()
    {
        var client = await StartAsync(app => app.MapQuery<FindWidgetCount, int?>("/widgets/countof/{id}"));

        var response = await client.GetAsync("/widgets/countof/0", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Given a MediatR handler that throws and no exception-handling middleware.
    /// When the endpoint is called.
    /// Then the original exception surfaces unwrapped to the host rather than being swallowed or
    /// translated by Postie or the adapter.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task UnhandledHandlerExceptionSurfacesUnwrappedThroughMediatR()
    {
        var client = await StartAsync(app => app.MapQuery<ExplodingRequest, Widget>("/widgets/explode/{reason}"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetAsync("/widgets/explode/boom", TestContext.Current.CancellationToken));

        Assert.Equal("boom", exception.Message);
    }
}
