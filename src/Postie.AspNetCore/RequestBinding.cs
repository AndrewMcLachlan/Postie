namespace Postie.AspNetCore;

/// <summary>
/// Controls how a command is built from the incoming HTTP request.
/// </summary>
/// <remarks>
/// The command-mapping methods default to <see cref="Body"/> for POST/PUT/PATCH and
/// <see cref="Parameters"/> for DELETE — the least-surprising choice for each verb. Override to mix
/// sources (for example an id from the route and a payload from the body) with <see cref="Parameters"/>
/// plus <c>[FromRoute]</c>/<c>[FromBody]</c> attributes on the command's properties.
/// </remarks>
public enum RequestBinding
{
    /// <summary>
    /// No binding attribute is applied; the framework's default inference decides. For a complex type
    /// this is usually the request body, unless the type defines <c>BindAsync</c> or <c>TryParse</c>.
    /// Member binding-source attributes (<c>[FromRoute]</c> and similar) are ignored when inference
    /// binds the whole command from the body; use <see cref="Parameters"/> for hybrid binding.
    /// </summary>
    Default = 0,

    /// <summary>
    /// The whole command is bound from the JSON request body with
    /// <see cref="Microsoft.AspNetCore.Mvc.FromBodyAttribute"/>.
    /// </summary>
    Body = 1,

    /// <summary>
    /// The command is bound from route, query and header values with
    /// <see cref="Microsoft.AspNetCore.Http.AsParametersAttribute"/>. Combine with per-property
    /// attributes to bind different members from different sources, including a hybrid route + body.
    /// </summary>
    Parameters = 2,
}
