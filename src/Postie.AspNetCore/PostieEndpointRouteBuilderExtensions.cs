namespace Postie.AspNetCore;

/// <summary>
/// Maps CQRS commands and queries to minimal API endpoints. Dispatch goes through the registered
/// <see cref="IEndpointDispatcher"/>, so these methods work with any mediator that has an adapter.
/// </summary>
/// <remarks>
/// Command-mapping methods take a <see cref="RequestBinding"/> that defaults to <see cref="RequestBinding.Body"/>
/// for POST/PUT/PATCH and <see cref="RequestBinding.Parameters"/> for DELETE. Queries always bind from
/// route/query/header values. Each method returns the <see cref="RouteHandlerBuilder"/> so the endpoint
/// can be customised further (<c>WithName</c>, <c>RequireAuthorization</c>, and so on).
/// </remarks>
public static class PostieEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps a GET request to a query. The query is bound from route, query and header values. A null
    /// result returns 404 Not Found; any other result returns 200 OK.
    /// </summary>
    /// <typeparam name="TRequest">The type of the query.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add the route to.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customise the endpoint.</returns>
    public static RouteHandlerBuilder MapQuery<TRequest, TResponse>(this IEndpointRouteBuilder endpoints, string pattern) where TRequest : notnull
    {
        var builder = endpoints.MapGet(pattern, EndpointHandlers.Query<TRequest, TResponse>())
                               .Produces<TResponse>();

        // A value-type response can never be null, so only reference-type queries can take the
        // null-to-404 path and only they advertise it.
        if (!typeof(TResponse).IsValueType)
        {
            builder.Produces(StatusCodes.Status404NotFound);
        }

        return builder;
    }

    /// <summary>
    /// Maps a GET request to a streaming query, returning the results as an asynchronous stream. The query
    /// is bound from route, query and header values.
    /// </summary>
    /// <remarks>
    /// Requires an <see cref="IStreamEndpointDispatcher"/> to be registered (the Postie and MediatR
    /// adapters register one automatically).
    /// </remarks>
    /// <typeparam name="TRequest">The type of the query.</typeparam>
    /// <typeparam name="TResponse">The type of each streamed item.</typeparam>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add the route to.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customise the endpoint.</returns>
    public static RouteHandlerBuilder MapStreamQuery<TRequest, TResponse>(this IEndpointRouteBuilder endpoints, string pattern) where TRequest : notnull =>
        endpoints.MapGet(pattern, EndpointHandlers.StreamQuery<TRequest, TResponse>())
                 .Produces<IEnumerable<TResponse>>();

    /// <summary>
    /// Maps a POST request to a command and returns its response.
    /// </summary>
    /// <typeparam name="TRequest">The type of the command.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add the route to.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="statusCode">The success status code for the response body. Defaults to 200 OK.</param>
    /// <param name="binding">How the command is bound. Defaults to <see cref="RequestBinding.Body"/>.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customise the endpoint.</returns>
    public static RouteHandlerBuilder MapCommand<TRequest, TResponse>(this IEndpointRouteBuilder endpoints, string pattern, int statusCode = StatusCodes.Status200OK, RequestBinding binding = RequestBinding.Body) where TRequest : notnull =>
        endpoints.MapPost(pattern, EndpointHandlers.Command<TRequest, TResponse>(statusCode, binding))
                 .Produces<TResponse>(statusCode);

    /// <summary>
    /// Maps a POST request to a command that returns no body, responding with <paramref name="statusCode"/>
    /// (204 No Content by default).
    /// </summary>
    /// <typeparam name="TRequest">The type of the command.</typeparam>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add the route to.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="statusCode">The status code to return. Defaults to 204 No Content.</param>
    /// <param name="binding">How the command is bound. Defaults to <see cref="RequestBinding.Body"/>.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customise the endpoint.</returns>
    public static RouteHandlerBuilder MapCommand<TRequest>(this IEndpointRouteBuilder endpoints, string pattern, int statusCode = StatusCodes.Status204NoContent, RequestBinding binding = RequestBinding.Body) where TRequest : notnull =>
        endpoints.MapPost(pattern, EndpointHandlers.VoidCommand<TRequest>(statusCode, binding))
                 .Produces(statusCode);

    /// <summary>
    /// Maps a PUT request to a command and returns its response.
    /// </summary>
    /// <typeparam name="TRequest">The type of the command.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add the route to.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="statusCode">The success status code for the response body. Defaults to 200 OK.</param>
    /// <param name="binding">How the command is bound. Defaults to <see cref="RequestBinding.Body"/>.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customise the endpoint.</returns>
    public static RouteHandlerBuilder MapPutCommand<TRequest, TResponse>(this IEndpointRouteBuilder endpoints, string pattern, int statusCode = StatusCodes.Status200OK, RequestBinding binding = RequestBinding.Body) where TRequest : notnull =>
        endpoints.MapPut(pattern, EndpointHandlers.Command<TRequest, TResponse>(statusCode, binding))
                 .Produces<TResponse>(statusCode);

    /// <summary>
    /// Maps a PUT request to a command that returns no body, responding with <paramref name="statusCode"/>
    /// (204 No Content by default).
    /// </summary>
    /// <typeparam name="TRequest">The type of the command.</typeparam>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add the route to.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="statusCode">The status code to return. Defaults to 204 No Content.</param>
    /// <param name="binding">How the command is bound. Defaults to <see cref="RequestBinding.Body"/>.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customise the endpoint.</returns>
    public static RouteHandlerBuilder MapPutCommand<TRequest>(this IEndpointRouteBuilder endpoints, string pattern, int statusCode = StatusCodes.Status204NoContent, RequestBinding binding = RequestBinding.Body) where TRequest : notnull =>
        endpoints.MapPut(pattern, EndpointHandlers.VoidCommand<TRequest>(statusCode, binding))
                 .Produces(statusCode);

    /// <summary>
    /// Maps a PATCH request to a command and returns its response.
    /// </summary>
    /// <typeparam name="TRequest">The type of the command.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add the route to.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="statusCode">The success status code for the response body. Defaults to 200 OK.</param>
    /// <param name="binding">How the command is bound. Defaults to <see cref="RequestBinding.Body"/>.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customise the endpoint.</returns>
    public static RouteHandlerBuilder MapPatchCommand<TRequest, TResponse>(this IEndpointRouteBuilder endpoints, string pattern, int statusCode = StatusCodes.Status200OK, RequestBinding binding = RequestBinding.Body) where TRequest : notnull =>
        endpoints.MapPatch(pattern, EndpointHandlers.Command<TRequest, TResponse>(statusCode, binding))
                 .Produces<TResponse>(statusCode);

    /// <summary>
    /// Maps a POST request to a command that creates a resource, returning 201 Created with a
    /// <c>Location</c> header pointing at the named route.
    /// </summary>
    /// <typeparam name="TRequest">The type of the command.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add the route to.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="routeName">The name of the route that retrieves the created resource.</param>
    /// <param name="getRouteValues">Builds the route values for the <c>Location</c> header from the response.</param>
    /// <param name="binding">How the command is bound. Defaults to <see cref="RequestBinding.Body"/>.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customise the endpoint.</returns>
    public static RouteHandlerBuilder MapPostCreate<TRequest, TResponse>(this IEndpointRouteBuilder endpoints, string pattern, string routeName, Func<TResponse, object?> getRouteValues, RequestBinding binding = RequestBinding.Body) where TRequest : notnull =>
        endpoints.MapPostCreate<TRequest, TResponse>(pattern, routeName, (_, response) => getRouteValues(response), binding);

    /// <summary>
    /// Maps a POST request to a command that creates a resource, returning 201 Created with a
    /// <c>Location</c> header pointing at the named route.
    /// </summary>
    /// <typeparam name="TRequest">The type of the command.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add the route to.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="routeName">The name of the route that retrieves the created resource.</param>
    /// <param name="getRouteValues">Builds the route values for the <c>Location</c> header from the command and the response.</param>
    /// <param name="binding">How the command is bound. Defaults to <see cref="RequestBinding.Body"/>.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customise the endpoint.</returns>
    public static RouteHandlerBuilder MapPostCreate<TRequest, TResponse>(this IEndpointRouteBuilder endpoints, string pattern, string routeName, Func<TRequest, TResponse, object?> getRouteValues, RequestBinding binding = RequestBinding.Body) where TRequest : notnull =>
        endpoints.MapPost(pattern, EndpointHandlers.Create<TRequest, TResponse>(routeName, getRouteValues, binding))
                 .Produces<TResponse>(StatusCodes.Status201Created);

    /// <summary>
    /// Maps a PUT request to a command that creates a resource, returning 201 Created with a
    /// <c>Location</c> header pointing at the named route.
    /// </summary>
    /// <typeparam name="TRequest">The type of the command.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add the route to.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="routeName">The name of the route that retrieves the created resource.</param>
    /// <param name="getRouteValues">Builds the route values for the <c>Location</c> header from the response.</param>
    /// <param name="binding">How the command is bound. Defaults to <see cref="RequestBinding.Body"/>.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customise the endpoint.</returns>
    public static RouteHandlerBuilder MapPutCreate<TRequest, TResponse>(this IEndpointRouteBuilder endpoints, string pattern, string routeName, Func<TResponse, object?> getRouteValues, RequestBinding binding = RequestBinding.Body) where TRequest : notnull =>
        endpoints.MapPutCreate<TRequest, TResponse>(pattern, routeName, (_, response) => getRouteValues(response), binding);

    /// <summary>
    /// Maps a PUT request to a command that creates a resource, returning 201 Created with a
    /// <c>Location</c> header pointing at the named route. With PUT the resource identity usually
    /// comes from the client, so the route values can be built from the command as well as the response.
    /// </summary>
    /// <typeparam name="TRequest">The type of the command.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add the route to.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="routeName">The name of the route that retrieves the created resource.</param>
    /// <param name="getRouteValues">Builds the route values for the <c>Location</c> header from the command and the response.</param>
    /// <param name="binding">How the command is bound. Defaults to <see cref="RequestBinding.Body"/>.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customise the endpoint.</returns>
    public static RouteHandlerBuilder MapPutCreate<TRequest, TResponse>(this IEndpointRouteBuilder endpoints, string pattern, string routeName, Func<TRequest, TResponse, object?> getRouteValues, RequestBinding binding = RequestBinding.Body) where TRequest : notnull =>
        endpoints.MapPut(pattern, EndpointHandlers.Create<TRequest, TResponse>(routeName, getRouteValues, binding))
                 .Produces<TResponse>(StatusCodes.Status201Created);

    /// <summary>
    /// Maps a DELETE request to a command that deletes a resource, returning 204 No Content.
    /// </summary>
    /// <typeparam name="TRequest">The type of the command.</typeparam>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add the route to.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="binding">How the command is bound. Defaults to <see cref="RequestBinding.Parameters"/>.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customise the endpoint.</returns>
    public static RouteHandlerBuilder MapDeleteCommand<TRequest>(this IEndpointRouteBuilder endpoints, string pattern, RequestBinding binding = RequestBinding.Parameters) where TRequest : notnull =>
        endpoints.MapDelete(pattern, EndpointHandlers.VoidCommand<TRequest>(StatusCodes.Status204NoContent, binding))
                 .Produces(StatusCodes.Status204NoContent);

    /// <summary>
    /// Maps a DELETE request to a command that deletes a resource and returns a response.
    /// </summary>
    /// <typeparam name="TRequest">The type of the command.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add the route to.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="statusCode">The success status code for the response body. Defaults to 200 OK.</param>
    /// <param name="binding">How the command is bound. Defaults to <see cref="RequestBinding.Parameters"/>.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customise the endpoint.</returns>
    public static RouteHandlerBuilder MapDeleteCommand<TRequest, TResponse>(this IEndpointRouteBuilder endpoints, string pattern, int statusCode = StatusCodes.Status200OK, RequestBinding binding = RequestBinding.Parameters) where TRequest : notnull =>
        endpoints.MapDelete(pattern, EndpointHandlers.Command<TRequest, TResponse>(statusCode, binding))
                 .Produces<TResponse>(statusCode);
}
