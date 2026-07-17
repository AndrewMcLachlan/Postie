using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace Postie.Sample.Orders.MediatR;

/// <summary>
/// Runs every registered validator for the request before the handler and throws
/// <see cref="ValidationException"/> with all failures. Postie.Cqrs.FluentValidation ships behaviors
/// like this for Postie's own mediator; MediatR pipelines need their own, so the sample carries one.
/// </summary>
public class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var context = new ValidationContext<TRequest>(request);

        List<ValidationFailure> failures = [];
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(context, cancellationToken);
            if (!result.IsValid)
            {
                failures.AddRange(result.Errors);
            }
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next();
    }
}
