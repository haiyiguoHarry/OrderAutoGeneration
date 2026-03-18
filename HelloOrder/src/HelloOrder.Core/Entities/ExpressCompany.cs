namespace HelloOrder.Core.Entities;

/// <summary>快递公司主数据</summary>
public class ExpressCompany
{
    public Guid Id { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = null!;
    public string? Contact { get; set; }
    public string? Remark { get; set; }
    public int Status { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<ExpressCountryRate> CountryRates { get; set; } = new List<ExpressCountryRate>();
}
