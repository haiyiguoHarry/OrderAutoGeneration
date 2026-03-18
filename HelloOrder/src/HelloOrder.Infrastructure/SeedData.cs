using HelloOrder.Core.Entities;
using HelloOrder.Core.Enums;
using HelloOrder.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HelloOrder.Infrastructure;

public static class SeedData
{
    public static async Task SeedAsync(this AppDbContext db, CancellationToken ct = default)
    {
        if (!await db.SysRoles.AnyAsync(ct))
        {
            var roles = new[]
            {
                new SysRole { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Code = "Admin", Name = "系统管理员", DataScope = DataScope.All, CreatedAt = DateTime.UtcNow },
                new SysRole { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Code = "Boss", Name = "老板", DataScope = DataScope.All, CreatedAt = DateTime.UtcNow },
                new SysRole { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Code = "Business", Name = "外贸业务员", DataScope = DataScope.Self, CreatedAt = DateTime.UtcNow },
                new SysRole { Id = Guid.Parse("44444444-4444-4444-4444-444444444444"), Code = "Assistant", Name = "助理", DataScope = DataScope.Self, CreatedAt = DateTime.UtcNow }
            };
            await db.SysRoles.AddRangeAsync(roles, ct);

            var adminUser = new SysUser
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Username = "admin",
                PasswordHash = AuthService.HashPasswordForCreate("admin123"),
                RealName = "系统管理员",
                RoleId = roles[0].Id,
                Status = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await db.SysUsers.AddAsync(adminUser, ct);
        }

        if (!await db.SysPermissions.AnyAsync(ct))
        {
            var perms = new[]
            {
                new SysPermission { Id = Guid.Parse("e1000001-0001-0001-0001-000000000001"), Code = "menu:dashboard", Name = "工作台", Type = PermissionType.Menu, ParentId = null, Path = "/dashboard", Sort = 1 },
                new SysPermission { Id = Guid.Parse("e1000002-0002-0002-0002-000000000002"), Code = "menu:merchants", Name = "商家管理", Type = PermissionType.Menu, ParentId = null, Path = "/merchants", Sort = 2 },
                new SysPermission { Id = Guid.Parse("e1000003-0003-0003-0003-000000000003"), Code = "menu:orders", Name = "订单管理", Type = PermissionType.Menu, ParentId = null, Path = "/orders", Sort = 3 },
                new SysPermission { Id = Guid.Parse("e1000004-0004-0004-0004-000000000004"), Code = "menu:system:users", Name = "用户管理", Type = PermissionType.Menu, ParentId = null, Path = "/system/users", Sort = 4 },
                new SysPermission { Id = Guid.Parse("e1000005-0005-0005-0005-000000000005"), Code = "menu:system:roles", Name = "角色管理", Type = PermissionType.Menu, ParentId = null, Path = "/system/roles", Sort = 5 }
            };
            await db.SysPermissions.AddRangeAsync(perms, ct);

            var adminRoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            foreach (var p in perms)
                await db.SysRolePermissions.AddAsync(new SysRolePermission { RoleId = adminRoleId, PermissionId = p.Id }, ct);
        }

        await db.SaveChangesAsync(ct);
    }
}
