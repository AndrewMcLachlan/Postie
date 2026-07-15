using Microsoft.AspNetCore.Mvc;

namespace Postie.AspNetCore;

/// <summary>
/// Builds the minimal API handler delegates that bind a request, dispatch it through
/// <see cref="IEndpointDispatcher"/>, and shape the HTTP result.
/// </summary>
internal static class EndpointHandlers
{
    internal static Delegate Query<TRequest, TResponse>() where TRequest : notnull =>
        async ([AsParameters] TRequest request, IEndpointDispatcher dispatcher, CancellationToken cancellationToken) =>
            Results.Ok(await dispatcher.DispatchAsync<TResponse>(request, cancellationToken));

    internal static Delegate StreamQuery<TRequest, TResponse>() where TRequest : notnull =>
        ([AsParameters] TRequest request, IStreamEndpointDispatcher dispatcher, CancellationToken cancellationToken) =>
            dispatcher.DispatchStream<TResponse>(request, cancellationToken);

    internal static Delegate Command<TRequest, TResponse>(int statusCode, RequestBinding binding) where TRequest : notnull =>
        Bind<TRequest>(
            async (request, dispatcher, cancellationToken) =>
            {
                var result = await dispatcher.DispatchAsync<TResponse>(request, cancellationToken);
                // 200 uses the idiomatic typed Ok result; other codes (e.g. 202 Accepted with a body)
                // go through Json so the requested status is honoured.
                return statusCode == StatusCodes.Status200OK
                    ? Results.Ok(result)
                    : Results.Json(result, statusCode: statusCode);
            },
            binding);

    internal static Delegate VoidCommand<TRequest>(int statusCode, RequestBinding binding) where TRequest : notnull =>
        Bind<TRequest>(
            async (request, dispatcher, cancellationToken) =>
            {
                await dispatcher.DispatchAsync(request, cancellationToken);
                return Results.StatusCode(statusCode);
            },
            binding);

    internal static Delegate Create<TRequest, TResponse>(string routeName, Func<TRequest, TResponse, object?> getRouteValues, RequestBinding binding) where TRequest : notnull =>
        Bind<TRequest>(
            async (request, dispatcher, cancellationToken) =>
            {
                var result = await dispatcher.DispatchAsync<TResponse>(request, cancellationToken);
                return Results.CreatedAtRoute(routeName, getRouteValues(request, result), result);
            },
            binding);

    // Applies the requested binding by re-declaring the request parameter with the matching attribute.
    // The attribute must sit on the delegate's parameter for minimal API binding to honour it, so a
    // separate lambda is produced per binding rather than branching inside one handler.
    private static Delegate Bind<TRequest>(Func<TRequest, IEndpointDispatcher, CancellationToken, Task<IResult>> core, RequestBinding binding) where TRequest : notnull =>
        binding switch
        {
            RequestBinding.Body => ([FromBody] TRequest request, IEndpointDispatcher dispatcher, CancellationToken cancellationToken) => core(request, dispatcher, cancellationToken),
            RequestBinding.Parameters => ([AsParameters] TRequest request, IEndpointDispatcher dispatcher, CancellationToken cancellationToken) => core(request, dispatcher, cancellationToken),
            _ => (TRequest request, IEndpointDispatcher dispatcher, CancellationToken cancellationToken) => core(request, dispatcher, cancellationToken),
        };
}
