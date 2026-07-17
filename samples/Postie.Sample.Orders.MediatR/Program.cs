using FluentValidation;
using MediatR;
using Postie.AspNetCore;
using Postie.Sample.Orders.MediatR;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<OrderStore>();

// MediatR as the mediator; Postie only maps the endpoints.
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
builder.Services.AddPostieMediatR();

// FluentValidation: validators + a MediatR pipeline behavior that throws ValidationException,
// and Postie's exception handler that turns it into a 400 problem-details response.
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddPostieValidationExceptionHandler();

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.MapOpenApi();
app.MapScalarApiReference();   // interactive API reference at /scalar

var orders = app.MapGroup("/orders");
orders.MapQuery<GetOrders, IReadOnlyList<Order>>("/");
orders.MapStreamQuery<StreamOrders, Order>("/stream");
orders.MapQuery<GetOrder, Order?>("/{id:int}").WithName("GetOrder");
orders.MapPostCreate<CreateOrder, Order>("/", "GetOrder", o => new { id = o.Id })
      .ProducesValidationProblem();   // advertise the 400 the validation pipeline produces
orders.MapPutCommand<UpdateOrder, Order>("/{id:int}", binding: RequestBinding.Parameters)
      .ProducesValidationProblem();
orders.MapDeleteCommand<DeleteOrder>("/{id:int}");

app.Run();
