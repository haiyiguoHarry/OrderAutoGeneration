namespace HelloOrder.Core.Entities;

/// <summary>Tracking&Cost 文件中的一个 Sheet（订单明细）</summary>
public class ConversionTrackingSheet
{
    public Guid Id { get; set; }
    public Guid ConversionJobId { get; set; }
    public string SheetName { get; set; } = null!;
    /// <summary>表头列名 JSON 数组</summary>
    public string? ColumnNamesJson { get; set; }
    /// <summary>行数据 JSON 数组</summary>
    public string ContentJson { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    public ConversionJob ConversionJob { get; set; } = null!;
}
