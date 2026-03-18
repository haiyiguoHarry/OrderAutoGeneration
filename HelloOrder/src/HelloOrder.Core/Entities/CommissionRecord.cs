namespace HelloOrder.Core.Entities;

public class CommissionRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string PeriodType { get; set; } = "month";
    public string PeriodValue { get; set; } = null!;
    public decimal SalesAmount { get; set; }
    public decimal CostAmount { get; set; }
    public decimal ProfitAmount { get; set; }
    public decimal CommissionAmount { get; set; }
    public int Status { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? Remark { get; set; }
}
