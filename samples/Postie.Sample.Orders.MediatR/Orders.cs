using System.Runtime.CompilerServices;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Postie.Sample.Orders.MediatR;

// Plain MediatR request types — no Postie interfaces.

public record GetOrders : IRequest<IReadOnlyList<Order>>;

// Nullable response: a null result becomes 404 Not Found.
public record GetOrder(int Id) : IRequest<Order?>;

public record CreateOrder(string Customer, decimal Total) : IRequest<Order>;

// Hybrid binding: id from the route, payload from the body — mapped with RequestBinding.Parameters.
public record UpdateOrder([FromRoute] int Id, [FromBody] OrderDetails Details) : IRequest<Order>;

public record DeleteOrder(int Id) : IRequest;

public record StreamOrders : IStreamRequest<Order>;

public class GetOrdersHandler(OrderStore store) : IRequestHandler<GetOrders, IReadOnlyList<Order>>
{
    public Task<IReadOnlyList<Order>> Handle(GetOrders request, CancellationToken cancellationToken) =>
        Task.FromResult(store.GetAll());
}

public class GetOrderHandler(OrderStore store) : IRequestHandler<GetOrder, Order?>
{
    public Task<Order?> Handle(GetOrder request, CancellationToken cancellationToken) =>
        Task.FromResult(store.Get(request.Id));
}

public class CreateOrderHandler(OrderStore store) : IRequestHandler<CreateOrder, Order>
{
    public Task<Order> Handle(CreateOrder request, CancellationToken cancellationToken) =>
        Task.FromResult(store.Add(request.Customer, request.Total));
}

public class UpdateOrderHandler(OrderStore store) : IRequestHandler<UpdateOrder, Order>
{
    public Task<Order> Handle(UpdateOrder request, CancellationToken cancellationToken) =>
        Task.FromResult(store.Update(request.Id, request.Details) ?? throw new KeyNotFoundException($"Order {request.Id} not found."));
}

public class DeleteOrderHandler(OrderStore store) : IRequestHandler<DeleteOrder>
{
    public Task Handle(DeleteOrder request, CancellationToken cancellationToken)
    {
        store.Delete(request.Id);
        return Task.CompletedTask;
    }
}

public class StreamOrdersHandler(OrderStore store) : IStreamRequestHandler<StreamOrders, Order>
{
    public async IAsyncEnumerable<Order> Handle(StreamOrders request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var order in store.GetAll())
        {
            await Task.Delay(500, cancellationToken);
            yield return order;
        }
    }
}

public class CreateOrderValidator : AbstractValidator<CreateOrder>
{
    public CreateOrderValidator()
    {
        RuleFor(c => c.Customer).NotEmpty();
        RuleFor(c => c.Total).GreaterThan(0);
    }
}

public class UpdateOrderValidator : AbstractValidator<UpdateOrder>
{
    public UpdateOrderValidator()
    {
        RuleFor(c => c.Details.Customer).NotEmpty();
        RuleFor(c => c.Details.Total).GreaterThan(0);
    }
}
