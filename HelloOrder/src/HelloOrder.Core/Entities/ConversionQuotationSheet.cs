namespace HelloOrder.Core.Entities;

/// <summary>报价新文件中的一个 Sheet：找货表 / 主报价表 / upsell 报价表</summary>
public class ConversionQuotationSheet
{
    public Guid Id { get; set; }
    public Guid ConversionJobId { get; set; }
    public string SheetName { get; set; } = null!;
    /// <summary>0=找货表 1=主报价表 2=upsell报价表</summary>
    public int SheetType { get; set; }
    /// <summary>表头列名 JSON 数组</summary>
    public string? ColumnNamesJson { get; set; }
    /// <summary>行数据 JSON 数组，每项为列值数组或对象</summary>
    public string ContentJson { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    public ConversionJob ConversionJob { get; set; } = null!;
}
