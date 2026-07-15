using Microsoft.Extensions.DependencyInjection;
using Postie.Cqrs;
using Postie.Cqrs.Commands;
using Postie.Cqrs.Queries;
using Postie.Cqrs.Tests.Commands;
using Postie.Cqrs.Tests.Queries;

namespace Postie.Cqrs.Tests;

public class PipelineBehaviorTests
{
    // Shared execution log so tests can assert the order behaviors and handlers run in.
    private sealed class Recorder
    {
        public List<string> Entries { get; } = [];
    }

    private sealed class OuterQueryBehavior<TQuery, TResponse>(Recorder recorder) : IQueryPipelineBehavior<TQuery, TResponse> where TQuery : IQuery<TResponse>
    {
        public async ValueTask<TResponse> Handle(TQuery query, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            recorder.Entries.Add("outer:before");
            var response = await next();
            recorder.Entries.Add("outer:after");
            return response;
        }
    }

    private sealed class InnerQueryBehavior<TQuery, TResponse>(Recorder recorder) : IQueryPipelineBehavior<TQuery, TResponse> where TQuery : IQuery<TResponse>
    {
        public async ValueTask<TResponse> Handle(TQuery query, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            recorder.Entries.Add("inner:before");
            var response = await next();
            recorder.Entries.Add("inner:after");
            return response;
        }
    }

    private sealed class AuditCommandBehavior<TCommand>(Recorder recorder) : ICommandPipelineBehavior<TCommand> where TCommand : ICommand
    {
        public async ValueTask Handle(TCommand command, RequestHandlerDelegate next, CancellationToken cancellationToken)
        {
            recorder.Entries.Add("audit:before");
            await next();
            recorder.Entries.Add("audit:after");
        }
    }

    private sealed class RecordingCommandBehavior<TCommand, TResponse>(Recorder recorder) : ICommandPipelineBehavior<TCommand, TResponse> where TCommand : ICommand<TResponse>
    {
        public async ValueTask<TResponse> Handle(TCommand command, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            recorder.Entries.Add("command:before");
            var response = await next();
            recorder.Entries.Add("command:after");
            return response;
        }
    }

    // A closed behavior (not an open generic) targeting one specific command.
    private sealed class ClosedTestCommandBehavior(Recorder recorder) : ICommandPipelineBehavior<TestCommand, bool>
    {
        public async ValueTask<bool> Handle(TestCommand command, RequestHandlerDelegate<bool> next, CancellationToken cancellationToken)
        {
            recorder.Entries.Add("closed:before");
            var response = await next();
            recorder.Entries.Add("closed:after");
            return response;
        }
    }

    /// <summary>
    /// Given two query behaviors registered in order.
    /// When a query is dispatched.
    /// Then the behaviors nest around the handler in registration order and the response is returned.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task QueryBehaviorsNestInRegistrationOrder()
    {
        var recorder = new Recorder();
        ServiceCollection services = new();
        services.AddSingleton(recorder);
        services.AddQueryHandlers(typeof(TestQuery).Assembly);
        services.AddQueryPipelineBehavior(typeof(OuterQueryBehavior<,>));
        services.AddQueryPipelineBehavior(typeof(InnerQueryBehavior<,>));
        var dispatcher = services.BuildServiceProvider().GetRequiredService<IQueryDispatcher>();

        var result = await dispatcher.Dispatch(new TestQuery { Input = "abc" }, TestContext.Current.CancellationToken);

        Assert.Equal("ABC", result);
        Assert.Equal(["outer:before", "inner:before", "inner:after", "outer:after"], recorder.Entries);
    }

    /// <summary>
    /// Given a behavior on a no-response command.
    /// When the command is executed.
    /// Then the behavior surrounds the handler.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task VoidCommandBehaviorSurroundsHandler()
    {
        var recorder = new Recorder();
        ServiceCollection services = new();
        services.AddSingleton(recorder);
        services.AddCommandHandlers(typeof(ThrowingCommand).Assembly);
        services.AddCommandPipelineBehavior(typeof(AuditCommandBehavior<>));
        var dispatcher = services.BuildServiceProvider().GetRequiredService<ICommandDispatcher>();

        await dispatcher.Execute(new NoOpCommand(), TestContext.Current.CancellationToken);

        Assert.Equal(["audit:before", "audit:after"], recorder.Entries);
    }

    /// <summary>
    /// Given an open-generic behavior on a command that returns a response.
    /// When the command is dispatched.
    /// Then the behavior surrounds the handler and the response flows back.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task CommandResponseBehaviorSurroundsHandler()
    {
        var recorder = new Recorder();
        ServiceCollection services = new();
        services.AddSingleton(recorder);
        services.AddCommandHandlers(typeof(TestCommand).Assembly);
        services.AddCommandPipelineBehavior(typeof(RecordingCommandBehavior<,>));
        var dispatcher = services.BuildServiceProvider().GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.Dispatch(new TestCommand { Input = "ABC" }, TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.Equal(["command:before", "command:after"], recorder.Entries);
    }

    /// <summary>
    /// Given a closed (non-generic) command behavior registered for one command type.
    /// When that command is dispatched.
    /// Then the closed behavior runs around the handler.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ClosedCommandBehaviorIsResolvedAndRun()
    {
        var recorder = new Recorder();
        ServiceCollection services = new();
        services.AddSingleton(recorder);
        services.AddCommandHandlers(typeof(TestCommand).Assembly);
        services.AddCommandPipelineBehavior(typeof(ClosedTestCommandBehavior));
        var dispatcher = services.BuildServiceProvider().GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.Dispatch(new TestCommand { Input = "ABC" }, TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.Equal(["closed:before", "closed:after"], recorder.Entries);
    }

    /// <summary>
    /// Given no behaviors registered.
    /// When a query is dispatched.
    /// Then the handler still runs and returns its response (the no-behavior fast path).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task QueryWithoutBehaviorsStillDispatches()
    {
        ServiceCollection services = new();
        services.AddQueryHandlers(typeof(TestQuery).Assembly);
        var dispatcher = services.BuildServiceProvider().GetRequiredService<IQueryDispatcher>();

        var result = await dispatcher.Dispatch(new TestQuery { Input = "xyz" }, TestContext.Current.CancellationToken);

        Assert.Equal("XYZ", result);
    }
}
