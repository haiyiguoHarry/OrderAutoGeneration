namespace HelloOrder.Core.Entities;

/// <summary>商家店铺：一个商家可有多个店铺，订单归属到具体店铺</summary>
public class MerchantShop
{
    public Guid Id { get; set; }
    public Guid MerchantId { get; set; }
    public string Name { get; set; } = null!;
    public string? Platform { get; set; }
    public string? ShopUrl { get; set; }
    public string? Remark { get; set; }
    public int Status { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Merchant Merchant { get; set; } = null!;
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
