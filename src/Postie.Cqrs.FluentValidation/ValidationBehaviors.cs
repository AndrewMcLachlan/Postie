using FluentValidation;
using FluentValidation.Results;
using Postie.Cqrs;
using Postie.Cqrs.Commands;
using Postie.Cqrs.Queries;

namespace Postie.Cqrs.FluentValidation;

/// <summary>
/// Runs the registered FluentValidation validators for a request, throwing a
/// <see cref="ValidationException"/> if any fail.
/// </summary>
internal static class RequestValidator
{
    internal static async ValueTask ValidateAsync<TRequest>(IEnumerable<IValidator<TRequest>> validators, TRequest request, CancellationToken cancellationToken)
    {
        // Materialise once; most requests have zero or one validator.
        var applicable = validators as IReadOnlyList<IValidator<TRequest>> ?? [.. validators];
        if (applicable.Count == 0)
        {
            return;
        }

        var context = new ValidationContext<TRequest>(request);

        List<ValidationFailure>? failures = null;

        foreach (var validator in applicable)
        {
            var result = await validator.ValidateAsync(context, cancellationToken);
            if (!result.IsValid)
            {
                (failures ??= []).AddRange(result.Errors);
            }
        }

        if (failures is { Count: > 0 })
        {
            throw new ValidationException(failures);
        }
    }
}

/// <summary>
/// Validates a query before the handler runs.
/// </summary>
/// <typeparam name="TQuery">The type of the query.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public sealed class QueryValidationBehavior<TQuery, TResponse>(IEnumerable<IValidator<TQuery>> validators) : IQueryPipelineBehavior<TQuery, TResponse> where TQuery : IQuery<TResponse>
{
    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(TQuery query, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        await RequestValidator.ValidateAsync(validators, query, cancellationToken);
        return await next();
    }
}

/// <summary>
/// Validates a command that returns a response before the handler runs.
/// </summary>
/// <typeparam name="TCommand">The type of the command.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public sealed class CommandValidationBehavior<TCommand, TResponse>(IEnumerable<IValidator<TCommand>> validators) : ICommandPipelineBehavior<TCommand, TResponse> where TCommand : ICommand<TResponse>
{
    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(TCommand command, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        await RequestValidator.ValidateAsync(validators, command, cancellationToken);
        return await next();
    }
}

/// <summary>
/// Validates a command that returns no response before the handler runs.
/// </summary>
/// <typeparam name="TCommand">The type of the command.</typeparam>
public sealed class CommandValidationBehavior<TCommand>(IEnumerable<IValidator<TCommand>> validators) : ICommandPipelineBehavior<TCommand> where TCommand : ICommand
{
    /// <inheritdoc />
    public async ValueTask Handle(TCommand command, RequestHandlerDelegate next, CancellationToken cancellationToken)
    {
        await RequestValidator.ValidateAsync(validators, command, cancellationToken);
        await next();
    }
}
