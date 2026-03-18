namespace HelloOrder.Application.Services;

/// <summary>订单文件转换：解析 Excel 入库、执行转换、导出 Excel</summary>
public interface IOrderConversionService
{
    /// <summary>解析报价新 Excel 并保存到任务的 QuotationSheets</summary>
    Task ParseAndSaveQuotationAsync(Guid jobId, Stream xlsxStream, string? fileName, CancellationToken ct = default);

    /// <summary>解析 Tracking&Cost Excel 并保存到任务的 TrackingSheets</summary>
    Task ParseAndSaveTrackingAsync(Guid jobId, Stream xlsxStream, string? fileName, CancellationToken ct = default);

    /// <summary>执行转换，生成三份 Excel 并返回文件流（onlyShip, total, purchase）</summary>
    Task<(Stream? onlyShip, Stream? total, Stream? purchase)> ConvertAsync(Guid jobId, CancellationToken ct = default);

    /// <summary>将转换后的 Tracking 数据同步到 Order/OrderItem 表，便于订单管理页展示</summary>
    Task<SyncToOrdersResult> SyncConvertedOrdersToDbAsync(Guid jobId, CancellationToken ct = default);
}

public class SyncToOrdersResult
{
    public int OrdersCreated { get; set; }
    public int OrdersUpdated { get; set; }
    public int OrderItemsCreated { get; set; }
    public string? Message { get; set; }
}
