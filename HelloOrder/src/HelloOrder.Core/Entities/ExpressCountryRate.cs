namespace HelloOrder.Core.Entities;

/// <summary>快递公司按国家运费/时效</summary>
public class ExpressCountryRate
{
    public Guid Id { get; set; }
    public Guid ExpressCompanyId { get; set; }
    public string CountryCode { get; set; } = null!;
    public string? CountryName { get; set; }
    public decimal? UnitPrice { get; set; }
    public int? LeadDaysMin { get; set; }
    public int? LeadDaysMax { get; set; }
    public string? ChargeRule { get; set; }
    public string? Remark { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ExpressCompany ExpressCompany { get; set; } = null!;
}
