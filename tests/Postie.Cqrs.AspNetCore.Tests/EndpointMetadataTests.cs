using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Postie.AspNetCore;

namespace Postie.Cqrs.AspNetCore.Tests;

public class EndpointMetadataTests
{
    private static Endpoint EndpointFor(Action<IEndpointRouteBuilder> map)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddPostie(typeof(GetGreeting).Assembly);
        var app = builder.Build();
        map(app);

        return ((IEndpointRouteBuilder)app).DataSources.SelectMany(ds => ds.Endpoints).Single();
    }

    private static IReadOnlyList<IProducesResponseTypeMetadata> ProducesFor(Action<IEndpointRouteBuilder> map) =>
        EndpointFor(map).Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>();

    /// <summary>
    /// Given a query with a reference-type response.
    /// When it is mapped with MapQuery.
    /// Then the endpoint advertises 404 (the engine's null-to-404 path can fire).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void MapQueryAdvertises404ForReferenceTypeResponse()
    {
        var produces = ProducesFor(app => app.MapQuery<FindWidget, Widget>("/widgets/find/{id}"));

        Assert.Contains(produces, m => m.StatusCode == StatusCodes.Status404NotFound);
    }

    /// <summary>
    /// Given a query with a value-type response.
    /// When it is mapped with MapQuery.
    /// Then the endpoint does not advertise 404 (a value type can never be null).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void MapQueryDoesNotAdvertise404ForValueTypeResponse()
    {
        var produces = ProducesFor(app => app.MapQuery<CountWidgets, int>("/widgets/count/{seed}"));

        Assert.DoesNotContain(produces, m => m.StatusCode == StatusCodes.Status404NotFound);
    }

    /// <summary>
    /// Given a query with a nullable value-type response.
    /// When it is mapped with MapQuery.
    /// Then the endpoint advertises 404 (a nullable value type can take the null-to-404 path).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void MapQueryAdvertises404ForNullableValueTypeResponse()
    {
        var produces = ProducesFor(app => app.MapQuery<FindWidgetCount, int?>("/widgets/countof/{id}"));

        Assert.Contains(produces, m => m.StatusCode == StatusCodes.Status404NotFound);
    }

    /// <summary>
    /// Given a command mapped with MapCommand.
    /// When its endpoint metadata is inspected.
    /// Then no 400 response is advertised — Postie never produces one itself; consumers who wire
    /// validation chain ProducesValidationProblem on the returned builder.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void MapCommandDoesNotAdvertiseValidationProblemByDefault()
    {
        var produces = ProducesFor(app => app.MapCommand<SubmitWidget, Widget>("/widgets"));

        Assert.DoesNotContain(produces, m => m.StatusCode == StatusCodes.Status400BadRequest);
    }

    /// <summary>
    /// Given a query mapped with QueryMethod.Post.
    /// When its endpoint metadata is inspected.
    /// Then the endpoint routes the POST method only.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void MapQueryWithPostRoutesPostMethod()
    {
        var endpoint = EndpointFor(app => app.MapQuery<SearchWidgets, Widget>("/widgets/search", QueryMethod.Post));

        Assert.Equal(["POST"], endpoint.Metadata.GetMetadata<HttpMethodMetadata>().HttpMethods);
    }

    /// <summary>
    /// Given a query mapped with QueryMethod.Query.
    /// When its endpoint metadata is inspected.
    /// Then the endpoint routes the QUERY method only.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void MapQueryWithQueryVerbRoutesQueryMethod()
    {
        var endpoint = EndpointFor(app => app.MapQuery<SearchWidgets, Widget>("/widgets/search", QueryMethod.Query));

        Assert.Equal(["QUERY"], endpoint.Metadata.GetMetadata<HttpMethodMetadata>().HttpMethods);
    }

    /// <summary>
    /// Given a POST query with a reference-type response.
    /// When its endpoint metadata is inspected.
    /// Then 404 is advertised — the null-to-404 convention is verb-independent.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void MapQueryWithPostAdvertises404ForReferenceTypeResponse()
    {
        var produces = ProducesFor(app => app.MapQuery<SearchWidgets, Widget>("/widgets/search", QueryMethod.Post));

        Assert.Contains(produces, m => m.StatusCode == StatusCodes.Status404NotFound);
    }

    /// <summary>
    /// Given a streaming query mapped with QueryMethod.Post.
    /// When its endpoint metadata is inspected.
    /// Then the endpoint routes the POST method only and advertises the streamed item collection.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void MapStreamQueryWithPostRoutesPostMethodAndAdvertisesCollection()
    {
        var endpoint = EndpointFor(app => app.MapStreamQuery<StreamMatchingWidgets, Widget>("/widgets/export", QueryMethod.Post));

        Assert.Equal(["POST"], endpoint.Metadata.GetMetadata<HttpMethodMetadata>().HttpMethods);
        Assert.Contains(
            endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>(),
            m => m.Type == typeof(IEnumerable<Widget>));
    }
}
