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

    public record DeleteUser(int Id) : ICommand;

    public class DeleteUserHandler : ICommandHandler<DeleteUser>
    {
        public ValueTask Handle(DeleteUser command, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    public class DeleteUserValidator : AbstractValidator<DeleteUser>
    {
        public DeleteUserValidator() => RuleFor(c => c.Id).GreaterThan(0);
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
    /// Given a no-response command that fails its validator.
    /// When it is executed.
    /// Then a ValidationException is thrown and the handler does not run.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InvalidVoidCommandThrowsValidationException()
    {
        var dispatcher = CommandDispatcher();

        await Assert.ThrowsAsync<ValidationException>(
            async () => await dispatcher.Execute(new DeleteUser(0), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Given a no-response command that passes its validator.
    /// When it is executed.
    /// Then the handler runs to completion.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidVoidCommandReachesHandler()
    {
        var dispatcher = CommandDispatcher();

        await dispatcher.Execute(new DeleteUser(5), TestContext.Current.CancellationToken);
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

    /// <summary>
    /// Given no assemblies passed to AddPostieValidation.
    /// When registration runs.
    /// Then an ArgumentException directs the caller to pass an assembly or use the generic overload.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void AddPostieValidationWithNoAssembliesThrows()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentException>(() => services.AddPostieValidation());

        Assert.Equal("assemblies", exception.ParamName);
    }

    /// <summary>
    /// Given a marker type from the validators assembly.
    /// When AddPostieValidation is called with the generic marker overload.
    /// Then validators from the marker's assembly are registered.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void AddPostieValidationWithMarkerRegistersValidators()
    {
        var services = new ServiceCollection();

        services.AddPostieValidation<CreateUserValidator>();

        Assert.Contains(services, s => s.ServiceType.IsGenericType && s.ServiceType.GetGenericTypeDefinition() == typeof(IValidator<>));
    }
}
