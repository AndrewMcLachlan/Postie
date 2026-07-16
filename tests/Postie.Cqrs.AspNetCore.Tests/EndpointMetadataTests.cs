using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Postie.AspNetCore;

namespace Postie.Cqrs.AspNetCore.Tests;

public class EndpointMetadataTests
{
    private static IReadOnlyList<IProducesResponseTypeMetadata> ProducesFor(Action<IEndpointRouteBuilder> map)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddPostie(typeof(GetGreeting).Assembly);
        var app = builder.Build();
        map(app);

        var endpoint = ((IEndpointRouteBuilder)app).DataSources.SelectMany(ds => ds.Endpoints).Single();
        return endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>();
    }

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
}
