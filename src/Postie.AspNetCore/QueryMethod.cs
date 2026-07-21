namespace Postie.AspNetCore;

/// <summary>
/// The HTTP method a query endpoint is mapped with.
/// </summary>
public enum QueryMethod
{
    /// <summary>GET. The query binds from route, query and header values by default.</summary>
    Get,

    /// <summary>POST. The query binds from the request body by default.</summary>
    Post,

    /// <summary>
    /// The HTTP QUERY method — safe and idempotent like GET, body-carrying like POST. The query
    /// binds from the request body by default. Client, intermediary and OpenAPI support for the
    /// method is still maturing.
    /// </summary>
    Query,
}
