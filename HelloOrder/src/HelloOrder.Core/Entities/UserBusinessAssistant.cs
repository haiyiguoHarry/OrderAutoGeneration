namespace HelloOrder.Core.Entities;

/// <summary>助理-业务员关联：助理可查看并操作该业务员下的数据</summary>
public class UserBusinessAssistant
{
    public Guid BusinessUserId { get; set; }
    public Guid AssistantUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public SysUser BusinessUser { get; set; } = null!;
    public SysUser AssistantUser { get; set; } = null!;
}
