namespace HelloOrder.Core.Enums;

/// <summary>提成规则类型</summary>
public enum CommissionRuleType
{
    BySalesRate = 0,   // 按销售额比例
    ByProfitRate = 1,  // 按利润比例
    ByTier = 2         // 阶梯
}
