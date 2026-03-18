namespace HelloOrder.Core.Enums;

/// <summary>采购单状态</summary>
public enum PurchaseOrderStatus
{
    Draft = 0,
    Issued = 1,
    Purchasing = 2,
    PartiallyReceived = 3,
    Received = 4
}
