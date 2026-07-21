namespace Postie.AspNetCore;

/// <summary>
/// Maps CQRS commands and queries to minimal API endpoints. Dispatch goes through the registered
/// <see cref="IEndpointDispatcher"/>, so these methods work with any mediator that has an adapter.
/// </summary>
/// <remarks>
/// Command-mapping methods take a <see cref="RequestBinding"/> that defaults to <see cref="RequestBinding.Body"/>
/// for POST/PUT/PATCH and <see cref="RequestBinding.Parameters"/> for DELETE. Query-mapping methods take a
/// <see cref="QueryMethod"/> selecting the HTTP method (GET by default) and bind idiomatically for it —
/// route/query/header values for GET, the request body for POST and QUERY — unless an explicit
/// <see cref="RequestBinding"/> overrides that. Each method returns the <see cref="RouteHandlerBuilder"/> so the
/// endpoint can be customised further (<c>WithName</c>, <c>RequireAuthorization</c>, and so on).
/// </remarks>
public static class PostieEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps a query to an endpoint. By default the endpoint is a GET bound from route, query and
    /// header values; <paramref name="method"/> selects POST or the HTTP QUERY method instead, both
    /// of which bind the query from the request body by default. A null result returns 404 Not Found;
    /// any other result returns 200 OK, whichever method is used.
    /// </summary>
    /// <typeparam name="TRequest">The type of the query.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add the route to.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="method">The HTTP method to map. Defaults to <see cref="QueryMethod.Get"/>.</param>
    /// <param name="binding">
    /// How the query is bound. Defaults to the idiomatic binding for <paramref name="method"/>:
    /// <see cref="RequestBinding.Parameters"/> for GET, <see cref="RequestBinding.Body"/> for POST
    /// and QUERY.
    /// </param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customise the endpoint.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="endpoints"/> or <paramref name="pattern"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="method"/> is not a defined <see cref="QueryMethod"/> value, or <paramref name="binding"/> is not a defined <see cref="RequestBinding"/> value.</exception>
    public static RouteHandlerBuilder MapQuery<TRequest, TResponse>(this IEndpointRouteBuilder endpoints, string pattern, QueryMethod method = QueryMethod.Get, RequestBinding? binding = null) where TRequest : notnull
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(pattern);
        ValidateQueryMethod(method);

        var builder = MapQueryVerb(endpoints, pattern, method, EndpointHandlers.Query<TRequest, TResponse>(ResolveQueryBinding(method, binding)))
                        .Produces<TResponse>();

        // A non-nullable value type can never be null, so only reference-type and Nullable<T>
        // queries can take the null-to-404 path and only they advertise it.
        if (!typeof(TResponse).IsValueType || Nullable.GetUnderlyingType(typeof(TResponse)) is not null)
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
    /// <exception cref="ArgumentNullException"><paramref name="endpoints"/> or <paramref name="pattern"/> is null.</exception>
    public static RouteHandlerBuilder MapStreamQuery<TRequest, TResponse>(this IEndpointRouteBuilder endpoints, string pattern) where TRequest : notnull
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(pattern);

        return endpoints.MapGet(pattern, EndpointHandlers.StreamQuery<TRequest, TResponse>())
                         .Produces<IEnumerable<TResponse>>();
    }

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
    /// <exception cref="ArgumentNullException"><paramref name="endpoints"/> or <paramref name="pattern"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="binding"/> is not a defined <see cref="RequestBinding"/> value.</exception>
    public static RouteHandlerBuilder MapCommand<TRequest, TResponse>(this IEndpointRouteBuilder endpoints, string pattern, int statusCode = StatusCodes.Status200OK, RequestBinding binding = RequestBinding.Body) where TRequest : notnull
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(pattern);
        ValidateBinding(binding);

        return endpoints.MapPost(pattern, EndpointHandlers.Command<TRequest, TResponse>(statusCode, binding))
                         .Produces<TResponse>(statusCode);
    }

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
    /// <exception cref="ArgumentNullException"><paramref name="endpoints"/> or <paramref name="pattern"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="binding"/> is not a defined <see cref="RequestBinding"/> value.</exception>
    public static RouteHandlerBuilder MapCommand<TRequest>(this IEndpointRouteBuilder endpoints, string pattern, int statusCode = StatusCodes.Status204NoContent, RequestBinding binding = RequestBinding.Body) where TRequest : notnull
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(pattern);
        ValidateBinding(binding);

        return endpoints.MapPost(pattern, EndpointHandlers.VoidCommand<TRequest>(statusCode, binding))
                         .Produces(statusCode);
    }

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
    /// <exception cref="ArgumentNullException"><paramref name="endpoints"/> or <paramref name="pattern"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="binding"/> is not a defined <see cref="RequestBinding"/> value.</exception>
    public static RouteHandlerBuilder MapPutCommand<TRequest, TResponse>(this IEndpointRouteBuilder endpoints, string pattern, int statusCode = StatusCodes.Status200OK, RequestBinding binding = RequestBinding.Body) where TRequest : notnull
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(pattern);
        ValidateBinding(binding);

        return endpoints.MapPut(pattern, EndpointHandlers.Command<TRequest, TResponse>(statusCode, binding))
                         .Produces<TResponse>(statusCode);
    }

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
    /// <exception cref="ArgumentNullException"><paramref name="endpoints"/> or <paramref name="pattern"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="binding"/> is not a defined <see cref="RequestBinding"/> value.</exception>
    public static RouteHandlerBuilder MapPutCommand<TRequest>(this IEndpointRouteBuilder endpoints, string pattern, int statusCode = StatusCodes.Status204NoContent, RequestBinding binding = RequestBinding.Body) where TRequest : notnull
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(pattern);
        ValidateBinding(binding);

        return endpoints.MapPut(pattern, EndpointHandlers.VoidCommand<TRequest>(statusCode, binding))
                         .Produces(statusCode);
    }

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
    /// <exception cref="ArgumentNullException"><paramref name="endpoints"/> or <paramref name="pattern"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="binding"/> is not a defined <see cref="RequestBinding"/> value.</exception>
    public static RouteHandlerBuilder MapPatchCommand<TRequest, TResponse>(this IEndpointRouteBuilder endpoints, string pattern, int statusCode = StatusCodes.Status200OK, RequestBinding binding = RequestBinding.Body) where TRequest : notnull
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(pattern);
        ValidateBinding(binding);

        return endpoints.MapPatch(pattern, EndpointHandlers.Command<TRequest, TResponse>(statusCode, binding))
                         .Produces<TResponse>(statusCode);
    }

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
    /// <exception cref="ArgumentNullException">
    /// <paramref name="getRouteValues"/> is null, or (via the target overload) <paramref name="endpoints"/>,
    /// <paramref name="pattern"/>, or <paramref name="routeName"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="routeName"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="binding"/> is not a defined <see cref="RequestBinding"/> value.</exception>
    public static RouteHandlerBuilder MapPostCreate<TRequest, TResponse>(this IEndpointRouteBuilder endpoints, string pattern, string routeName, Func<TResponse, object?> getRouteValues, RequestBinding binding = RequestBinding.Body) where TRequest : notnull
    {
        // getRouteValues is wrapped in a lambda before delegating, so a null check on the target overload
        // would never see the null — it would only surface once the wrapper runs, per request.
        ArgumentNullException.ThrowIfNull(getRouteValues);

        return endpoints.MapPostCreate<TRequest, TResponse>(pattern, routeName, (_, response) => getRouteValues(response), binding);
    }

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
    /// <exception cref="ArgumentNullException">
    /// <paramref name="endpoints"/>, <paramref name="pattern"/>, <paramref name="routeName"/>, or
    /// <paramref name="getRouteValues"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="routeName"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="binding"/> is not a defined <see cref="RequestBinding"/> value.</exception>
    public static RouteHandlerBuilder MapPostCreate<TRequest, TResponse>(this IEndpointRouteBuilder endpoints, string pattern, string routeName, Func<TRequest, TResponse, object?> getRouteValues, RequestBinding binding = RequestBinding.Body) where TRequest : notnull
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentException.ThrowIfNullOrEmpty(routeName);
        ArgumentNullException.ThrowIfNull(getRouteValues);
        ValidateBinding(binding);

        return endpoints.MapPost(pattern, EndpointHandlers.Create<TRequest, TResponse>(routeName, getRouteValues, binding))
                         .Produces<TResponse>(StatusCodes.Status201Created);
    }

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
    /// <exception cref="ArgumentNullException">
    /// <paramref name="getRouteValues"/> is null, or (via the target overload) <paramref name="endpoints"/>,
    /// <paramref name="pattern"/>, or <paramref name="routeName"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="routeName"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="binding"/> is not a defined <see cref="RequestBinding"/> value.</exception>
    public static RouteHandlerBuilder MapPutCreate<TRequest, TResponse>(this IEndpointRouteBuilder endpoints, string pattern, string routeName, Func<TResponse, object?> getRouteValues, RequestBinding binding = RequestBinding.Body) where TRequest : notnull
    {
        // getRouteValues is wrapped in a lambda before delegating, so a null check on the target overload
        // would never see the null — it would only surface once the wrapper runs, per request.
        ArgumentNullException.ThrowIfNull(getRouteValues);

        return endpoints.MapPutCreate<TRequest, TResponse>(pattern, routeName, (_, response) => getRouteValues(response), binding);
    }

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
    /// <exception cref="ArgumentNullException">
    /// <paramref name="endpoints"/>, <paramref name="pattern"/>, <paramref name="routeName"/>, or
    /// <paramref name="getRouteValues"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="routeName"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="binding"/> is not a defined <see cref="RequestBinding"/> value.</exception>
    public static RouteHandlerBuilder MapPutCreate<TRequest, TResponse>(this IEndpointRouteBuilder endpoints, string pattern, string routeName, Func<TRequest, TResponse, object?> getRouteValues, RequestBinding binding = RequestBinding.Body) where TRequest : notnull
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentException.ThrowIfNullOrEmpty(routeName);
        ArgumentNullException.ThrowIfNull(getRouteValues);
        ValidateBinding(binding);

        return endpoints.MapPut(pattern, EndpointHandlers.Create<TRequest, TResponse>(routeName, getRouteValues, binding))
                         .Produces<TResponse>(StatusCodes.Status201Created);
    }

    /// <summary>
    /// Maps a DELETE request to a command that deletes a resource, returning 204 No Content.
    /// </summary>
    /// <typeparam name="TRequest">The type of the command.</typeparam>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add the route to.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="binding">How the command is bound. Defaults to <see cref="RequestBinding.Parameters"/>.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customise the endpoint.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="endpoints"/> or <paramref name="pattern"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="binding"/> is not a defined <see cref="RequestBinding"/> value.</exception>
    public static RouteHandlerBuilder MapDeleteCommand<TRequest>(this IEndpointRouteBuilder endpoints, string pattern, RequestBinding binding = RequestBinding.Parameters) where TRequest : notnull
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(pattern);
        ValidateBinding(binding);

        return endpoints.MapDelete(pattern, EndpointHandlers.VoidCommand<TRequest>(StatusCodes.Status204NoContent, binding))
                         .Produces(StatusCodes.Status204NoContent);
    }

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
    /// <exception cref="ArgumentNullException"><paramref name="endpoints"/> or <paramref name="pattern"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="binding"/> is not a defined <see cref="RequestBinding"/> value.</exception>
    public static RouteHandlerBuilder MapDeleteCommand<TRequest, TResponse>(this IEndpointRouteBuilder endpoints, string pattern, int statusCode = StatusCodes.Status200OK, RequestBinding binding = RequestBinding.Parameters) where TRequest : notnull
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(pattern);
        ValidateBinding(binding);

        return endpoints.MapDelete(pattern, EndpointHandlers.Command<TRequest, TResponse>(statusCode, binding))
                         .Produces<TResponse>(statusCode);
    }

    // Rejects a RequestBinding value outside the defined set. Without this, an undefined value falls
    // through the discard arm of the switch in EndpointHandlers.Bind and silently gets Default binding,
    // instead of failing loudly at map time.
    private static void ValidateBinding(RequestBinding binding)
    {
        if (binding is not (RequestBinding.Default or RequestBinding.Body or RequestBinding.Parameters))
        {
            throw new ArgumentOutOfRangeException(nameof(binding), binding, $"'{binding}' is not a defined {nameof(RequestBinding)} value.");
        }
    }

    // The HTTP QUERY method has no Map{Verb} shorthand and no HttpMethods constant before .NET 10,
    // so the method string is pinned here for every target framework.
    private const string QueryHttpMethod = "QUERY";

    private static RouteHandlerBuilder MapQueryVerb(IEndpointRouteBuilder endpoints, string pattern, QueryMethod method, Delegate handler) =>
        method switch
        {
            QueryMethod.Get => endpoints.MapGet(pattern, handler),
            QueryMethod.Post => endpoints.MapPost(pattern, handler),
            _ => endpoints.MapMethods(pattern, [QueryHttpMethod], handler),
        };

    private static RequestBinding ResolveQueryBinding(QueryMethod method, RequestBinding? binding)
    {
        if (binding is { } explicitBinding)
        {
            ValidateBinding(explicitBinding);
            return explicitBinding;
        }

        return method == QueryMethod.Get ? RequestBinding.Parameters : RequestBinding.Body;
    }

    private static void ValidateQueryMethod(QueryMethod method)
    {
        if (method is not (QueryMethod.Get or QueryMethod.Post or QueryMethod.Query))
        {
            throw new ArgumentOutOfRangeException(nameof(method), method, $"'{method}' is not a defined {nameof(QueryMethod)} value.");
        }
    }
}
