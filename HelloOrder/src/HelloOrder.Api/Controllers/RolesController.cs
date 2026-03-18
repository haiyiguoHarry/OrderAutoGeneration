using System.Security.Claims;
using HelloOrder.Api.Common;
using HelloOrder.Core.Entities;
using HelloOrder.Core.Enums;
using HelloOrder.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelloOrder.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly AppDbContext _db;

    public RolesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<ApiResult<List<RoleDto>>>> Get(
        [FromQuery] string? name = null,
        [FromQuery] string? code = null,
        CancellationToken ct = default)
    {
        if (GetRoleCode() != "Admin") return Forbid();
        IQueryable<SysRole> q = _db.SysRoles.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(name))
            q = q.Where(r => r.Name.Contains(name));
        if (!string.IsNullOrWhiteSpace(code))
            q = q.Where(r => r.Code.Contains(code));
        var list = await q.OrderBy(r => r.Code)
            .Select(r => new RoleDto
            {
                Id = r.Id,
                Code = r.Code,
                Name = r.Name,
                DataScope = (int)r.DataScope,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(ct);
        return Ok(ApiResult<List<RoleDto>>.Ok(list));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResult<RoleDto>>> GetById(Guid id, CancellationToken ct = default)
    {
        if (GetRoleCode() != "Admin") return Forbid();
        var r = await _db.SysRoles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r == null) return NotFound();
        return Ok(ApiResult<RoleDto>.Ok(new RoleDto
        {
            Id = r.Id,
            Code = r.Code,
            Name = r.Name,
            DataScope = (int)r.DataScope,
            CreatedAt = r.CreatedAt
        }));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResult<Guid>>> Create([FromBody] RoleCreateDto dto, CancellationToken ct = default)
    {
        if (GetRoleCode() != "Admin") return Forbid();
        if (string.IsNullOrWhiteSpace(dto.Code)) return BadRequest(ApiResult<Guid>.Fail("角色编码不能为空"));
        var code = dto.Code.Trim();
        if (await _db.SysRoles.AnyAsync(r => r.Code == code, ct))
            return BadRequest(ApiResult<Guid>.Fail("角色编码已存在"));
        var role = new SysRole
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = (dto.Name ?? code).Trim(),
            DataScope = (DataScope)(dto.DataScope ?? 0),
            CreatedAt = DateTime.UtcNow
        };
        _db.SysRoles.Add(role);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResult<Guid>.Ok(role.Id));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResult<object>>> Update(Guid id, [FromBody] RoleUpdateDto dto, CancellationToken ct = default)
    {
        if (GetRoleCode() != "Admin") return Forbid();
        var role = await _db.SysRoles.FindAsync(new object[] { id }, ct);
        if (role == null) return NotFound();
        if (!string.IsNullOrWhiteSpace(dto.Name))
            role.Name = dto.Name.Trim();
        if (dto.DataScope.HasValue)
            role.DataScope = (DataScope)dto.DataScope.Value;
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResult<object>.Ok(new { }));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResult<object>>> Delete(Guid id, CancellationToken ct = default)
    {
        if (GetRoleCode() != "Admin") return Forbid();
        var role = await _db.SysRoles.FindAsync(new object[] { id }, ct);
        if (role == null) return NotFound();
        if (await _db.SysUsers.AnyAsync(u => u.RoleId == id, ct))
            return BadRequest(ApiResult<object>.Fail("该角色下还有用户，请先解除分配再删除"));
        _db.SysRolePermissions.RemoveRange(await _db.SysRolePermissions.Where(rp => rp.RoleId == id).ToListAsync(ct));
        _db.SysRoles.Remove(role);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResult<object>.Ok(new { }));
    }

    [HttpGet("{id}/permissions")]
    public async Task<ActionResult<ApiResult<List<Guid>>>> GetPermissions(Guid id, CancellationToken ct = default)
    {
        if (GetRoleCode() != "Admin") return Forbid();
        var list = await _db.SysRolePermissions
            .Where(rp => rp.RoleId == id)
            .Select(rp => rp.PermissionId)
            .ToListAsync(ct);
        return Ok(ApiResult<List<Guid>>.Ok(list));
    }

    [HttpPut("{id}/permissions")]
    public async Task<ActionResult<ApiResult<object>>> SetPermissions(Guid id, [FromBody] SetRolePermissionsDto dto, CancellationToken ct = default)
    {
        if (GetRoleCode() != "Admin") return Forbid();
        var exists = await _db.SysRoles.AnyAsync(r => r.Id == id, ct);
        if (!exists) return NotFound();
        var current = await _db.SysRolePermissions.Where(rp => rp.RoleId == id).ToListAsync(ct);
        _db.SysRolePermissions.RemoveRange(current);
        if (dto.PermissionIds != null && dto.PermissionIds.Count > 0)
        {
            var validIds = await _db.SysPermissions.Where(p => dto.PermissionIds.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct);
            foreach (var pid in validIds)
                _db.SysRolePermissions.Add(new SysRolePermission { RoleId = id, PermissionId = pid });
        }
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResult<object>.Ok(new { }));
    }

    private string? GetRoleCode() => User.FindFirstValue("role_code") ?? "";
}

public class RoleDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public int DataScope { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RoleCreateDto
{
    public string Code { get; set; } = null!;
    public string? Name { get; set; }
    public int? DataScope { get; set; }
}

public class RoleUpdateDto
{
    public string? Name { get; set; }
    public int? DataScope { get; set; }
}

public class SetRolePermissionsDto
{
    public List<Guid>? PermissionIds { get; set; }
}
