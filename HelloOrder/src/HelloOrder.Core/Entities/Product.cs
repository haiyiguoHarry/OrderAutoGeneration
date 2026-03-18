namespace HelloOrder.Core.Entities;

/// <summary>商品：店铺维度主数据，供找货表、报价、运费计算、采购引用</summary>
public class Product
{
    public Guid Id { get; set; }
    public Guid MerchantShopId { get; set; }
    public string? Sku { get; set; }
    public string Name { get; set; } = null!;
    /// <summary>英文名，用于报价表</summary>
    public string? NameEn { get; set; }
    public string? Spec { get; set; }
    public string? ImageUrl { get; set; }
    public string? PlatformUrl { get; set; }
    /// <summary>重量(kg)，用于运费计算</summary>
    public decimal? WeightKg { get; set; }
    /// <summary>重量(g)，找货表常用</summary>
    public decimal? WeightGrams { get; set; }
    /// <summary>包装尺寸 cm</summary>
    public decimal? LengthCm { get; set; }
    public decimal? WidthCm { get; set; }
    public decimal? HeightCm { get; set; }
    public string? Link1688 { get; set; }
    public string? CustomerLink { get; set; }
    public string? FactoryLink { get; set; }
    public string? Material { get; set; }
    public string? StyleName { get; set; }
    public string? SizeChart { get; set; }
    public string? PackageNote { get; set; }
    public decimal? SuggestedPurchasePrice { get; set; }
    public decimal? SuggestedSalePrice { get; set; }
    public string? Remark { get; set; }
    public int Status { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public MerchantShop MerchantShop { get; set; } = null!;
}
