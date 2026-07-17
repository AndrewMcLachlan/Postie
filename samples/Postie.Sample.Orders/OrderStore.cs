using System.Collections.Concurrent;

namespace Postie.Sample.Orders;

public record Order(int Id, string Customer, decimal Total);

public record OrderDetails(string Customer, decimal Total);

/// <summary>
/// In-memory order storage, seeded with a few orders so the API returns data immediately.
/// </summary>
public class OrderStore
{
    private readonly ConcurrentDictionary<int, Order> _orders = new();
    private int _nextId;

    public OrderStore()
    {
        Add("Ada Lovelace", 125.50m);
        Add("Charles Babbage", 78.20m);
        Add("Grace Hopper", 310.00m);
    }

    public IReadOnlyList<Order> GetAll() => [.. _orders.Values.OrderBy(o => o.Id)];

    public Order? Get(int id) => _orders.GetValueOrDefault(id);

    public Order Add(string customer, decimal total)
    {
        var order = new Order(Interlocked.Increment(ref _nextId), customer, total);
        _orders[order.Id] = order;
        return order;
    }

    public Order? Update(int id, OrderDetails details)
    {
        while (true)
        {
            if (!_orders.TryGetValue(id, out var existing))
            {
                return null;
            }

            var updated = existing with { Customer = details.Customer, Total = details.Total };
            if (_orders.TryUpdate(id, updated, existing))
            {
                return updated;
            }
        }
    }

    public bool Delete(int id) => _orders.TryRemove(id, out _);
}
