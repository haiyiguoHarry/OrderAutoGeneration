namespace HelloOrder.Core.Enums;

/// <summary>订单状态</summary>
public enum OrderStatus
{
    PendingQuote = 0,      // 待报价
    Quoted = 1,            // 已报价待确认
    Confirmed = 2,         // 已确认待付款
    Paid = 3,              // 已付款
    PurchaseGenerated = 4, // 已生成采购
    Purchasing = 5,        // 采购中
    ReadyToShip = 6,       // 到仓待发
    Shipped = 7,           // 已发货
    Completed = 8,        // 已完成
    AfterSales = 9         // 售后/异常
}
