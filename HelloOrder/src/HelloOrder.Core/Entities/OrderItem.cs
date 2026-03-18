namespace HelloOrder.Core.Entities;

public class OrderItem
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    /// <summary>关联商品（可选，便于带出 SKU/规格/重量等）</summary>
    public Guid? ProductId { get; set; }
    public string? Sku { get; set; }
    public string? ProductName { get; set; }
    public string? Spec { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public string? Link1688 { get; set; }
    public decimal? PurchasePrice { get; set; }
    public string? Remark { get; set; }

    public Order Order { get; set; } = null!;
    public Product? Product { get; set; }
}
