using HelloOrder.Core.Enums;

namespace HelloOrder.Core.Entities;

public class PurchaseOrder
{
    public Guid Id { get; set; }
    public string PurchaseNo { get; set; } = null!;
    public string SourceType { get; set; } = "order";
    public Guid? BusinessUserId { get; set; }
    /// <summary>操作人（生成/处理采购单的业务员或助理）</summary>
    public Guid? OperatorId { get; set; }
    public PurchaseOrderStatus Status { get; set; }
    public int TotalQuantity { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<PurchaseItem> Items { get; set; } = new List<PurchaseItem>();
}
