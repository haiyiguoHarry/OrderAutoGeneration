namespace HelloOrder.Core.Entities;

public class SysUser
{
    public Guid Id { get; set; }
    public string Username { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string RealName { get; set; } = null!;
    public Guid? RoleId { get; set; }
    public Guid? DeptId { get; set; }
    public int Status { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public SysRole? Role { get; set; }
}
