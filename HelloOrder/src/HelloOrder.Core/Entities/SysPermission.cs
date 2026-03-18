using HelloOrder.Core.Enums;

namespace HelloOrder.Core.Entities;

public class SysPermission
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public PermissionType Type { get; set; }
    public Guid? ParentId { get; set; }
    public string? Path { get; set; }
    public int Sort { get; set; }
}
