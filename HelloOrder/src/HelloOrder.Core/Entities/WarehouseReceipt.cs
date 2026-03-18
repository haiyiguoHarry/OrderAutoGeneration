namespace HelloOrder.Core.Entities;

public class WarehouseReceipt
{
    public Guid Id { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public string ReceiptNo { get; set; } = null!;
    public DateTime? ReceivedAt { get; set; }
    public Guid? OperatorId { get; set; }
    public int Status { get; set; }
}
