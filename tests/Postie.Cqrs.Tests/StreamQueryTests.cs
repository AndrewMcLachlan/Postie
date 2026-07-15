using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Postie.Cqrs;
using Postie.Cqrs.Queries;

namespace Postie.Cqrs.Tests;

public class StreamQueryTests
{
    public record Countdown(int From) : IStreamQuery<int>;

    public class CountdownHandler : IStreamQueryHandler<Countdown, int>
    {
        public async IAsyncEnumerable<int> Handle(Countdown query, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            for (var i = query.From; i >= 1; i--)
            {
                await Task.Yield();
                yield return i;
            }
        }
    }

    private sealed class DoublingBehavior<TQuery, TResponse>(List<string> log) : IStreamQueryPipelineBehavior<TQuery, TResponse> where TQuery : IStreamQuery<TResponse>
    {
        public async IAsyncEnumerable<TResponse> Handle(TQuery query, StreamHandlerDelegate<TResponse> next, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            log.Add("behavior:start");
            await foreach (var item in next().WithCancellation(cancellationToken))
            {
                yield return item;
            }
            log.Add("behavior:end");
        }
    }

    /// <summary>
    /// Given a registered stream query handler.
    /// When the query is dispatched and enumerated.
    /// Then the handler's items are streamed in order.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task StreamQueryStreamsHandlerItems()
    {
        ServiceCollection services = new();
        services.AddStreamQueryHandlers(typeof(Countdown).Assembly);
        var dispatcher = services.BuildServiceProvider().GetRequiredService<IStreamQueryDispatcher>();

        var items = new List<int>();
        await foreach (var item in dispatcher.Dispatch(new Countdown(3), TestContext.Current.CancellationToken))
        {
            items.Add(item);
        }

        Assert.Equal([3, 2, 1], items);
    }

    /// <summary>
    /// Given a stream behavior wrapping a stream query.
    /// When the query is enumerated.
    /// Then the behavior surrounds the stream and every item still flows through.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task StreamBehaviorWrapsTheStream()
    {
        var log = new List<string>();
        ServiceCollection services = new();
        services.AddSingleton(log);
        services.AddStreamQueryHandlers(typeof(Countdown).Assembly);
        services.AddStreamQueryPipelineBehavior(typeof(DoublingBehavior<,>));
        var dispatcher = services.BuildServiceProvider().GetRequiredService<IStreamQueryDispatcher>();

        var items = new List<int>();
        await foreach (var item in dispatcher.Dispatch(new Countdown(2), TestContext.Current.CancellationToken))
        {
            items.Add(item);
        }

        Assert.Equal([2, 1], items);
        Assert.Equal(["behavior:start", "behavior:end"], log);
    }
}
