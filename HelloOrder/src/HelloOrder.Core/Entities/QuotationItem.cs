namespace HelloOrder.Core.Entities;

public class QuotationItem
{
    public Guid Id { get; set; }
    public Guid QuotationId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? OrderItemId { get; set; }
    public string? Sku { get; set; }
    public string? ProductName { get; set; }
    public string? Spec { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public string? Link1688 { get; set; }

    public Quotation Quotation { get; set; } = null!;
}
