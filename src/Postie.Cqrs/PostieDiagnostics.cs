using System.Diagnostics;

namespace Postie.Cqrs;

/// <summary>
/// Diagnostics for Postie's dispatch. Add the source to an OpenTelemetry tracer to record a span per
/// dispatched query, command or stream query.
/// </summary>
/// <remarks>
/// <code>
/// tracerProviderBuilder.AddSource(PostieDiagnostics.ActivitySourceName);
/// </code>
/// </remarks>
public static class PostieDiagnostics
{
    /// <summary>
    /// The name of the <see cref="System.Diagnostics.ActivitySource"/> Postie dispatches under.
    /// </summary>
    public const string ActivitySourceName = "Postie.Cqrs";

    /// <summary>
    /// The <see cref="System.Diagnostics.ActivitySource"/> Postie creates dispatch activities on.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
