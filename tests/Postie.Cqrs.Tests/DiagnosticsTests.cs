using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Postie.Cqrs;
using Postie.Cqrs.Queries;
using Postie.Cqrs.Tests.Queries;

namespace Postie.Cqrs.Tests;

public class DiagnosticsTests
{
    // A dedicated query so the assertions can filter out activities from other tests: the listener is
    // process-wide and xUnit runs test classes in parallel.
    public record DiagnosticsPing : IQuery<string>;

    public class DiagnosticsPingHandler : IQueryHandler<DiagnosticsPing, string>
    {
        public ValueTask<string> Handle(DiagnosticsPing query, CancellationToken cancellationToken) => new("pong");
    }

    public record FailingPing : IQuery<string>;

    public class FailingPingHandler : IQueryHandler<FailingPing, string>
    {
        public ValueTask<string> Handle(FailingPing query, CancellationToken cancellationToken) => throw new InvalidOperationException("boom");
    }

    public record FailingStream : IStreamQuery<int>;

    public class FailingStreamHandler : IStreamQueryHandler<FailingStream, int>
    {
        public async IAsyncEnumerable<int> Handle(FailingStream query, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            yield return 1;
            throw new InvalidOperationException("boom");
        }
    }

    private static (ActivityListener listener, ConcurrentQueue<Activity> activities) Listen()
    {
        var activities = new ConcurrentQueue<Activity>();
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == PostieDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activities.Enqueue,
        };
        ActivitySource.AddActivityListener(listener);
        return (listener, activities);
    }

    /// <summary>
    /// Given a listener on Postie's activity source.
    /// When a query is dispatched.
    /// Then an activity is recorded, named for the request and tagged with its kind and type.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task DispatchEmitsActivityWithTags()
    {
        var activities = new ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == PostieDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activities.Enqueue,
        };
        ActivitySource.AddActivityListener(listener);

        ServiceCollection services = new();
        services.AddQueryHandler<DiagnosticsPingHandler, DiagnosticsPing, string>();
        var dispatcher = services.BuildServiceProvider().GetRequiredService<IQueryDispatcher>();

        await dispatcher.Dispatch(new DiagnosticsPing(), TestContext.Current.CancellationToken);

        var activity = Assert.Single(activities, a => a.DisplayName == $"Postie {nameof(DiagnosticsPing)}");
        Assert.Equal("query", activity.GetTagItem("postie.request_kind"));
        Assert.Equal(typeof(DiagnosticsPing).FullName, activity.GetTagItem("postie.request_type"));
    }

    /// <summary>
    /// Given a listener and a query whose handler throws.
    /// When the query is dispatched.
    /// Then the activity is marked with Error status.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task FailingDispatchMarksActivityError()
    {
        var (listener, activities) = Listen();
        using (listener)
        {
            ServiceCollection services = new();
            services.AddQueryHandler<FailingPingHandler, FailingPing, string>();
            var dispatcher = services.BuildServiceProvider().GetRequiredService<IQueryDispatcher>();

            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await dispatcher.Dispatch(new FailingPing(), TestContext.Current.CancellationToken));

            var activity = Assert.Single(activities, a => a.DisplayName == $"Postie {nameof(FailingPing)}");
            Assert.Equal(ActivityStatusCode.Error, activity.Status);
        }
    }

    /// <summary>
    /// Given a listener and a stream query whose handler throws mid-stream.
    /// When the stream is enumerated.
    /// Then the activity is marked with Error status.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task FailingStreamMarksActivityError()
    {
        var (listener, activities) = Listen();
        using (listener)
        {
            ServiceCollection services = new();
            services.AddStreamQueryHandler<FailingStreamHandler, FailingStream, int>();
            var dispatcher = services.BuildServiceProvider().GetRequiredService<IStreamQueryDispatcher>();

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await foreach (var _ in dispatcher.Dispatch(new FailingStream(), TestContext.Current.CancellationToken))
                {
                }
            });

            var activity = Assert.Single(activities, a => a.DisplayName == $"Postie {nameof(FailingStream)}");
            Assert.Equal(ActivityStatusCode.Error, activity.Status);
        }
    }

    /// <summary>
    /// Given no listener on Postie's activity source.
    /// When a query is dispatched.
    /// Then it still returns the handler's response (the no-listener fast path).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task DispatchWithoutListenerStillReturnsResponse()
    {
        ServiceCollection services = new();
        services.AddQueryHandlers(typeof(TestQuery).Assembly);
        var dispatcher = services.BuildServiceProvider().GetRequiredService<IQueryDispatcher>();

        var result = await dispatcher.Dispatch(new TestQuery { Input = "abc" }, TestContext.Current.CancellationToken);

        Assert.Equal("ABC", result);
    }
}
