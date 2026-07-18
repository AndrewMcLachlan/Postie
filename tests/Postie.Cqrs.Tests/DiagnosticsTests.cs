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

    public record DiagnosticsStream : IStreamQuery<int>;

    public class DiagnosticsStreamHandler : IStreamQueryHandler<DiagnosticsStream, int>
    {
        public async IAsyncEnumerable<int> Handle(DiagnosticsStream query, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            for (var i = 1; i <= 3; i++)
            {
                await Task.Delay(1, cancellationToken);
                yield return i;
            }
        }
    }

    // Never registered with a handler: dispatching it exercises the "no handler" synchronous failure
    // path (GetRequiredService throws) before any activity is created.
    public record UnregisteredStream : IStreamQuery<int>;

    public record CancellableStream : IStreamQuery<int>;

    public class CancellableStreamHandler : IStreamQueryHandler<CancellableStream, int>
    {
        public async IAsyncEnumerable<int> Handle(CancellableStream query, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return 1;
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return 2;
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
    /// Given a listener and a stream query.
    /// When the stream is dispatched but never enumerated.
    /// Then Activity.Current is unchanged and no Postie activity is stopped.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void DispatchWithoutEnumerationDoesNotAlterActivityCurrent()
    {
        var (listener, activities) = Listen();
        using (listener)
        {
            ServiceCollection services = new();
            services.AddStreamQueryHandler<DiagnosticsStreamHandler, DiagnosticsStream, int>();
            var dispatcher = services.BuildServiceProvider().GetRequiredService<IStreamQueryDispatcher>();

            var before = Activity.Current;

            _ = dispatcher.Dispatch(new DiagnosticsStream(), TestContext.Current.CancellationToken);

            Assert.Same(before, Activity.Current);
            Assert.DoesNotContain(activities, a => a.DisplayName == $"Postie {nameof(DiagnosticsStream)}");
        }
    }

    /// <summary>
    /// Given a listener and a stream query that is dispatched but not enumerated immediately.
    /// When enumeration eventually runs to completion.
    /// Then Activity.Current stayed unchanged until enumeration started, and exactly one activity is
    /// recorded with a non-error status.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task DelayedEnumerationStartsActivityOnFirstMoveNext()
    {
        var (listener, activities) = Listen();
        using (listener)
        {
            ServiceCollection services = new();
            services.AddStreamQueryHandler<DiagnosticsStreamHandler, DiagnosticsStream, int>();
            var dispatcher = services.BuildServiceProvider().GetRequiredService<IStreamQueryDispatcher>();

            var before = Activity.Current;
            var stream = dispatcher.Dispatch(new DiagnosticsStream(), TestContext.Current.CancellationToken);

            Assert.Same(before, Activity.Current);

            await foreach (var _ in stream)
            {
            }

            var activity = Assert.Single(activities, a => a.DisplayName == $"Postie {nameof(DiagnosticsStream)}");
            Assert.NotEqual(ActivityStatusCode.Error, activity.Status);
        }
    }

    /// <summary>
    /// Given a listener and a stream query whose handler is never registered.
    /// When the query is dispatched.
    /// Then the dispatch call throws synchronously (GetRequiredService fails before any activity
    /// exists) and Activity.Current is unchanged.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void SynchronousDispatchFailureDoesNotAlterActivityCurrent()
    {
        var (listener, activities) = Listen();
        using (listener)
        {
            ServiceCollection services = new();
            services.AddStreamQueryHandler<DiagnosticsStreamHandler, DiagnosticsStream, int>();
            var dispatcher = services.BuildServiceProvider().GetRequiredService<IStreamQueryDispatcher>();

            var before = Activity.Current;

            Assert.Throws<InvalidOperationException>(
                () => dispatcher.Dispatch(new UnregisteredStream(), TestContext.Current.CancellationToken));

            Assert.Same(before, Activity.Current);
            Assert.DoesNotContain(activities, a => a.DisplayName == $"Postie {nameof(UnregisteredStream)}");
        }
    }

    /// <summary>
    /// Given a listener and a stream query handler that completes successfully.
    /// When the stream is fully enumerated.
    /// Then the items are yielded, the activity is stopped with a duration covering enumeration, and
    /// its status is not Error.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task SuccessfulEnumerationStopsActivityWithNonErrorStatus()
    {
        var (listener, activities) = Listen();
        using (listener)
        {
            ServiceCollection services = new();
            services.AddStreamQueryHandler<DiagnosticsStreamHandler, DiagnosticsStream, int>();
            var dispatcher = services.BuildServiceProvider().GetRequiredService<IStreamQueryDispatcher>();

            var items = new List<int>();
            await foreach (var item in dispatcher.Dispatch(new DiagnosticsStream(), TestContext.Current.CancellationToken))
            {
                items.Add(item);
            }

            Assert.Equal([1, 2, 3], items);

            var activity = Assert.Single(activities, a => a.DisplayName == $"Postie {nameof(DiagnosticsStream)}");
            Assert.NotEqual(ActivityStatusCode.Error, activity.Status);
            Assert.True(activity.Duration > TimeSpan.Zero);
        }
    }

    /// <summary>
    /// Given a listener and a stream query that yields more than one item.
    /// When the enumerator is disposed after only the first item is read.
    /// Then the activity is still stopped (its finally block runs on early disposal).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task EarlyDisposalStopsActivity()
    {
        var (listener, activities) = Listen();
        using (listener)
        {
            ServiceCollection services = new();
            services.AddStreamQueryHandler<DiagnosticsStreamHandler, DiagnosticsStream, int>();
            var dispatcher = services.BuildServiceProvider().GetRequiredService<IStreamQueryDispatcher>();

            var stream = dispatcher.Dispatch(new DiagnosticsStream(), TestContext.Current.CancellationToken);

            await using (var enumerator = stream.GetAsyncEnumerator(TestContext.Current.CancellationToken))
            {
                Assert.True(await enumerator.MoveNextAsync());
                Assert.Equal(1, enumerator.Current);
            }

            var activity = Assert.Single(activities, a => a.DisplayName == $"Postie {nameof(DiagnosticsStream)}");
            Assert.NotNull(activity);
        }
    }

    /// <summary>
    /// Given a listener and a stream query being enumerated.
    /// When the cancellation token is cancelled mid-enumeration.
    /// Then OperationCanceledException propagates and the activity is stopped.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task CancellationMidEnumerationStopsActivity()
    {
        var (listener, activities) = Listen();
        using (listener)
        {
            ServiceCollection services = new();
            services.AddStreamQueryHandler<CancellableStreamHandler, CancellableStream, int>();
            var dispatcher = services.BuildServiceProvider().GetRequiredService<IStreamQueryDispatcher>();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await foreach (var item in dispatcher.Dispatch(new CancellableStream(), cts.Token))
                {
                    Assert.Equal(1, item);
                    cts.Cancel();
                }
            });

            var activity = Assert.Single(activities, a => a.DisplayName == $"Postie {nameof(CancellableStream)}");
            Assert.NotNull(activity);
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
