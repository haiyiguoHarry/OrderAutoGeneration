namespace HelloOrder.Core.Entities;

/// <summary>订单文件转换任务：一次转换对应一份报价新 + 一份 Tracking&Cost</summary>
public class ConversionJob
{
    public Guid Id { get; set; }
    /// <summary>任务标题，如 LG-Le 2026.1.22</summary>
    public string Title { get; set; } = null!;
    /// <summary>报价新文件原始名</summary>
    public string? QuotationFileName { get; set; }
    /// <summary>Tracking&Cost 文件原始名</summary>
    public string? TrackingFileName { get; set; }
    /// <summary>从文件名解析的日期，用于生成输出文件名</summary>
    public DateTime? ExtractedDate { get; set; }
    /// <summary>商户/目录标识，如 LG-Le</summary>
    public string? MerchantFolder { get; set; }
    /// <summary>关联商家（同步到订单时必填）</summary>
    public Guid? MerchantId { get; set; }
    /// <summary>关联店铺（可选）</summary>
    public Guid? MerchantShopId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? Remark { get; set; }

    public ICollection<ConversionQuotationSheet> QuotationSheets { get; set; } = new List<ConversionQuotationSheet>();
    public ICollection<ConversionTrackingSheet> TrackingSheets { get; set; } = new List<ConversionTrackingSheet>();
}
