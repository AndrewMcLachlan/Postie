using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;

namespace Postie.AspNetCore.FluentValidation;

/// <summary>
/// Maps FluentValidation's <see cref="ValidationException"/> to a 400 validation problem-details
/// response (RFC 9457), with the failure messages grouped by property name.
/// </summary>
internal sealed class ValidationExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ValidationException validationException)
        {
            return false;
        }

        var errors = validationException.Errors
            .GroupBy(e => e.PropertyName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray(), StringComparer.Ordinal);

        await Results.ValidationProblem(errors).ExecuteAsync(httpContext);
        return true;
    }
}
