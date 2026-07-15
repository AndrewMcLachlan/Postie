using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Postie.Cqrs.Commands;
using Postie.Cqrs.Queries;

namespace Postie.Cqrs.FluentValidation.Tests;

public class ValidationBehaviorTests
{
    public record CreateUser(string Name) : ICommand<int>;

    public class CreateUserHandler : ICommandHandler<CreateUser, int>
    {
        public ValueTask<int> Handle(CreateUser command, CancellationToken cancellationToken) => new(1);
    }

    public class CreateUserValidator : AbstractValidator<CreateUser>
    {
        public CreateUserValidator() => RuleFor(c => c.Name).NotEmpty();
    }

    public record GetUser(int Id) : IQuery<string>;

    public class GetUserHandler : IQueryHandler<GetUser, string>
    {
        public ValueTask<string> Handle(GetUser query, CancellationToken cancellationToken) => new("Ada");
    }

    public class GetUserValidator : AbstractValidator<GetUser>
    {
        public GetUserValidator() => RuleFor(q => q.Id).GreaterThan(0);
    }

    private static ICommandDispatcher CommandDispatcher()
    {
        ServiceCollection services = new();
        services.AddCommandHandlers(typeof(CreateUser).Assembly);
        services.AddPostieValidation(typeof(CreateUser).Assembly);
        return services.BuildServiceProvider().GetRequiredService<ICommandDispatcher>();
    }

    private static IQueryDispatcher QueryDispatcher()
    {
        ServiceCollection services = new();
        services.AddQueryHandlers(typeof(GetUser).Assembly);
        services.AddPostieValidation(typeof(GetUser).Assembly);
        return services.BuildServiceProvider().GetRequiredService<IQueryDispatcher>();
    }

    /// <summary>
    /// Given a command that fails its validator.
    /// When it is dispatched.
    /// Then a ValidationException is thrown and the handler does not run.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InvalidCommandThrowsValidationException()
    {
        var dispatcher = CommandDispatcher();

        await Assert.ThrowsAsync<ValidationException>(
            async () => await dispatcher.Dispatch(new CreateUser(""), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Given a command that passes its validator.
    /// When it is dispatched.
    /// Then the handler runs and returns its response.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidCommandReachesHandler()
    {
        var dispatcher = CommandDispatcher();

        var result = await dispatcher.Dispatch(new CreateUser("Ada"), TestContext.Current.CancellationToken);

        Assert.Equal(1, result);
    }

    /// <summary>
    /// Given a query that fails its validator.
    /// When it is dispatched.
    /// Then a ValidationException is thrown.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InvalidQueryThrowsValidationException()
    {
        var dispatcher = QueryDispatcher();

        await Assert.ThrowsAsync<ValidationException>(
            async () => await dispatcher.Dispatch(new GetUser(0), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Given a query that passes its validator.
    /// When it is dispatched.
    /// Then the handler runs and returns its response.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidQueryReachesHandler()
    {
        var dispatcher = QueryDispatcher();

        var result = await dispatcher.Dispatch(new GetUser(5), TestContext.Current.CancellationToken);

        Assert.Equal("Ada", result);
    }
}
