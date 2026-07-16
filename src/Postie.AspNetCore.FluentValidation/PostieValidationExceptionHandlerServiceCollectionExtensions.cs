using Postie.AspNetCore.FluentValidation;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration for the FluentValidation exception handler.
/// </summary>
public static class PostieValidationExceptionHandlerServiceCollectionExtensions
{
    /// <summary>
    /// Registers problem-details services and an <see cref="Microsoft.AspNetCore.Diagnostics.IExceptionHandler"/>
    /// that maps FluentValidation's <c>ValidationException</c> to a 400 validation problem-details
    /// response. Add <c>app.UseExceptionHandler()</c> to the pipeline for the handler to run.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddPostieValidationExceptionHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddProblemDetails();
        services.AddExceptionHandler<ValidationExceptionHandler>();

        return services;
    }
}
