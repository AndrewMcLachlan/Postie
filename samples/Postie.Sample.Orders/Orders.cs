using Microsoft.AspNetCore.Mvc;
using Postie.Cqrs.Commands;
using Postie.Cqrs.Queries;

namespace Postie.Sample.Orders;

public record GetOrders : IQuery<IReadOnlyList<Order>>;

// Nullable response: a null result becomes 404 Not Found.
public record GetOrder(int Id) : IQuery<Order?>;

public record CreateOrder(string Customer, decimal Total) : ICommand<Order>;

// Hybrid binding: id from the route, payload from the body — mapped with RequestBinding.Parameters.
public record UpdateOrder([FromRoute] int Id, [FromBody] OrderDetails Details) : ICommand<Order>;

public record DeleteOrder(int Id) : ICommand;

public class GetOrdersHandler(OrderStore store) : IQueryHandler<GetOrders, IReadOnlyList<Order>>
{
    public ValueTask<IReadOnlyList<Order>> Handle(GetOrders query, CancellationToken cancellationToken) =>
        new(store.GetAll());
}

public class GetOrderHandler(OrderStore store) : IQueryHandler<GetOrder, Order?>
{
    public ValueTask<Order?> Handle(GetOrder query, CancellationToken cancellationToken) =>
        new(store.Get(query.Id));
}

public class CreateOrderHandler(OrderStore store) : ICommandHandler<CreateOrder, Order>
{
    public ValueTask<Order> Handle(CreateOrder command, CancellationToken cancellationToken) =>
        new(store.Add(command.Customer, command.Total));
}

public class UpdateOrderHandler(OrderStore store) : ICommandHandler<UpdateOrder, Order>
{
    public ValueTask<Order> Handle(UpdateOrder command, CancellationToken cancellationToken) =>
        new(store.Update(command.Id, command.Details) ?? throw new KeyNotFoundException($"Order {command.Id} not found."));
}

public class DeleteOrderHandler(OrderStore store) : ICommandHandler<DeleteOrder>
{
    public ValueTask Handle(DeleteOrder command, CancellationToken cancellationToken)
    {
        store.Delete(command.Id);
        return ValueTask.CompletedTask;
    }
}
