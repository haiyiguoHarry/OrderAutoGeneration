using HelloOrder.Core.Enums;

namespace HelloOrder.Core.Entities;

public class CommissionRule
{
    public Guid Id { get; set; }
    public string RoleType { get; set; } = null!;
    public CommissionRuleType RuleType { get; set; }
    public string? Config { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}
