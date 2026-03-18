namespace HelloOrder.Core.Entities;

public class Merchant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Platform { get; set; }
    public string? ShopName { get; set; }
    public string? Contact { get; set; }
    public string? SettlementType { get; set; }
    public Guid? BusinessUserId { get; set; }
    public int Status { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public SysUser? BusinessUser { get; set; }
}
