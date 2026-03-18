namespace HelloOrder.Core.Entities;

public class Shipment
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string? ShipmentNo { get; set; }
    public string? ExpressCompany { get; set; }
    public string? ExpressNo { get; set; }
    public decimal? Weight { get; set; }
    public DateTime? PackedAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public Guid? OperatorId { get; set; }

    public Order Order { get; set; } = null!;
    public ICollection<ShipmentItem> Items { get; set; } = new List<ShipmentItem>();
}
