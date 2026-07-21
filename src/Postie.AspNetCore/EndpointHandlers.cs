using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace Postie.AspNetCore;

/// <summary>
/// Builds the minimal API handler delegates that bind a request, dispatch it through
/// <see cref="IEndpointDispatcher"/>, and shape the HTTP result.
/// </summary>
internal static class EndpointHandlers
{
    internal static Delegate Query<TRequest, TResponse>(RequestBinding binding) where TRequest : notnull =>
        Bind<TRequest>(
            async (request, dispatcher, cancellationToken) =>
            {
                var result = await dispatcher.DispatchAsync<TResponse>(request, cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            },
            binding);

    internal static Delegate StreamQuery<TRequest, TResponse>(RequestBinding binding) where TRequest : notnull =>
        BindStream<TRequest, TResponse>(
            static (request, dispatcher, cancellationToken) => dispatcher.DispatchStream<TResponse>(request, cancellationToken),
            binding);

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
    private static Delegate Bind<TRequest>(Func<TRequest, IEndpointDispatcher, CancellationToken, Task<IResult>> core, RequestBinding binding) where TRequest : notnull
    {
        if (binding == RequestBinding.Body)
        {
            GuardBodyBinding<TRequest>();
        }

        return binding switch
        {
            RequestBinding.Body => ([FromBody] TRequest request, IEndpointDispatcher dispatcher, CancellationToken cancellationToken) => core(request, dispatcher, cancellationToken),
            RequestBinding.Parameters => ([AsParameters] TRequest request, IEndpointDispatcher dispatcher, CancellationToken cancellationToken) => core(request, dispatcher, cancellationToken),
            _ => (TRequest request, IEndpointDispatcher dispatcher, CancellationToken cancellationToken) => core(request, dispatcher, cancellationToken),
        };
    }

    private static Delegate BindStream<TRequest, TResponse>(Func<TRequest, IStreamEndpointDispatcher, CancellationToken, IAsyncEnumerable<TResponse>> core, RequestBinding binding) where TRequest : notnull
    {
        if (binding == RequestBinding.Body)
        {
            GuardBodyBinding<TRequest>();
        }

        return binding switch
        {
            RequestBinding.Body => ([FromBody] TRequest request, IStreamEndpointDispatcher dispatcher, CancellationToken cancellationToken) => core(request, dispatcher, cancellationToken),
            RequestBinding.Parameters => ([AsParameters] TRequest request, IStreamEndpointDispatcher dispatcher, CancellationToken cancellationToken) => core(request, dispatcher, cancellationToken),
            _ => (TRequest request, IStreamEndpointDispatcher dispatcher, CancellationToken cancellationToken) => core(request, dispatcher, cancellationToken),
        };
    }

    // Body binding deserialises the whole command from the request body, so per-member binding-source
    // attributes are silently ignored — almost certainly a configuration mistake. Runs once per
    // endpoint at map time, so mapping fails at startup rather than mis-binding per request.
    private static void GuardBodyBinding<TRequest>()
    {
        List<string> offending = [];

        foreach (var property in typeof(TRequest).GetProperties())
        {
            if (HasBindingSourceAttribute(property))
            {
                offending.Add(property.Name);
            }
        }

        foreach (var constructor in typeof(TRequest).GetConstructors())
        {
            foreach (var parameter in constructor.GetParameters())
            {
                if (HasBindingSourceAttribute(parameter) && parameter.Name is not null && !offending.Contains(parameter.Name))
                {
                    offending.Add(parameter.Name);
                }
            }
        }

        if (offending.Count > 0)
        {
            throw new InvalidOperationException(
                $"Request type '{typeof(TRequest).Name}' is mapped with RequestBinding.Body, but member(s) {String.Join(", ", offending)} have [FromRoute]/[FromQuery]/[FromHeader]/[FromForm] attributes that are ignored when the whole request binds from the body. Map the endpoint with RequestBinding.Parameters to bind members from different sources.");
        }
    }

    private static bool HasBindingSourceAttribute(ICustomAttributeProvider member) =>
        member.IsDefined(typeof(FromRouteAttribute), inherit: true) ||
        member.IsDefined(typeof(FromQueryAttribute), inherit: true) ||
        member.IsDefined(typeof(FromHeaderAttribute), inherit: true) ||
        member.IsDefined(typeof(FromFormAttribute), inherit: true);
}
