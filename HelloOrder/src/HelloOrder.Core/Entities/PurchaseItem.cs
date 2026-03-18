namespace HelloOrder.Core.Entities;

public class PurchaseItem
{
    public Guid Id { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public string? Sku { get; set; }
    public string? ProductName { get; set; }
    public string? Spec { get; set; }
    public int Quantity { get; set; }
    public string? Link1688 { get; set; }
    public string? Supplier { get; set; }
    public string? ExpressNo { get; set; }
    public int ReceivedQty { get; set; }
    public string? OrderItemIds { get; set; }

    public PurchaseOrder PurchaseOrder { get; set; } = null!;
}
