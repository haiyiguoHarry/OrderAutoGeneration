using HelloOrder.Core.Enums;

namespace HelloOrder.Core.Entities;

public class Order
{
    public Guid Id { get; set; }
    public Guid MerchantId { get; set; }
    /// <summary>所属店铺（可选）</summary>
    public Guid? MerchantShopId { get; set; }
    public string OrderNo { get; set; } = null!;
    public string? PlatformOrderId { get; set; }
    public Guid? BusinessUserId { get; set; }
    public Guid? AssistantUserId { get; set; }
    /// <summary>最后操作人（业务员或助理）</summary>
    public Guid? LastOperatorId { get; set; }
    public OrderStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "CNY";
    public DateTime? OrderTime { get; set; }
    /// <summary>标记已付款时间（用于商家每日付款统计）</summary>
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    /// <summary>来自订单文件转换任务（若有）</summary>
    public Guid? ConversionJobId { get; set; }

    public Merchant Merchant { get; set; } = null!;
    public MerchantShop? MerchantShop { get; set; }
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
