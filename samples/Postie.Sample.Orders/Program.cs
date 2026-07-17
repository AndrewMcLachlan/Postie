using OpenTelemetry.Trace;
using Postie.AspNetCore;
using Postie.Cqrs;
using Postie.Sample.Orders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<OrderStore>();
builder.Services.AddPostie<Program>();

// Every dispatched query/command prints a "Postie <RequestType>" span to the console.
// AlwaysOnSampler: the default ParentBased sampler would otherwise drop these spans, because the
// incoming ASP.NET Core request Activity is unsampled here (no AspNetCore instrumentation registered)
// and its "do not record" decision propagates to Postie's child span per W3C trace-context rules.
builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing
    .SetSampler(new AlwaysOnSampler())
    .AddSource(PostieDiagnostics.ActivitySourceName)
    .AddConsoleExporter());

var app = builder.Build();

var orders = app.MapGroup("/orders");
orders.MapQuery<GetOrders, IReadOnlyList<Order>>("/");
orders.MapQuery<GetOrder, Order?>("/{id:int}").WithName("GetOrder");
orders.MapPostCreate<CreateOrder, Order>("/", "GetOrder", o => new { id = o.Id });
orders.MapPutCommand<UpdateOrder, Order>("/{id:int}", binding: RequestBinding.Parameters);
orders.MapDeleteCommand<DeleteOrder>("/{id:int}");

app.Run();
