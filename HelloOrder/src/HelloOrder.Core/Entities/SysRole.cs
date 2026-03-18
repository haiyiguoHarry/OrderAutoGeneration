using HelloOrder.Core.Enums;

namespace HelloOrder.Core.Entities;

public class SysRole
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public DataScope DataScope { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<SysRolePermission> RolePermissions { get; set; } = new List<SysRolePermission>();
}
