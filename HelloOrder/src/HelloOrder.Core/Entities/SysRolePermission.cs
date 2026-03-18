namespace HelloOrder.Core.Entities;

public class SysRolePermission
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }

    public SysRole Role { get; set; } = null!;
    public SysPermission Permission { get; set; } = null!;
}
