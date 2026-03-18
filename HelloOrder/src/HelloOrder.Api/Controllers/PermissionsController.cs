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
public class PermissionsController : ControllerBase
{
    private readonly AppDbContext _db;

    public PermissionsController(AppDbContext db) => _db = db;

    private bool IsAdmin => User.FindFirstValue("role_code") == "Admin";

    [HttpGet]
    public async Task<ActionResult<ApiResult<List<PermissionDto>>>> Get(
        [FromQuery] int? type = null,
        [FromQuery] string? name = null,
        [FromQuery] string? code = null,
        CancellationToken ct = default)
    {
        if (!IsAdmin) return Forbid();
        IQueryable<SysPermission> q = _db.SysPermissions.AsNoTracking();
        if (type.HasValue)
            q = q.Where(p => (int)p.Type == type.Value);
        if (!string.IsNullOrWhiteSpace(name))
            q = q.Where(p => p.Name.Contains(name));
        if (!string.IsNullOrWhiteSpace(code))
            q = q.Where(p => p.Code.Contains(code));
        var list = await q.OrderBy(p => p.Sort).ThenBy(p => p.Code)
            .Select(p => new PermissionDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                Type = (int)p.Type,
                ParentId = p.ParentId,
                Path = p.Path,
                Sort = p.Sort
            })
            .ToListAsync(ct);
        return Ok(ApiResult<List<PermissionDto>>.Ok(list));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResult<PermissionDto>>> GetById(Guid id, CancellationToken ct = default)
    {
        if (!IsAdmin) return Forbid();
        var p = await _db.SysPermissions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p == null) return NotFound();
        return Ok(ApiResult<PermissionDto>.Ok(new PermissionDto
        {
            Id = p.Id,
            Code = p.Code,
            Name = p.Name,
            Type = (int)p.Type,
            ParentId = p.ParentId,
            Path = p.Path,
            Sort = p.Sort
        }));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResult<Guid>>> Create([FromBody] PermissionCreateDto dto, CancellationToken ct = default)
    {
        if (!IsAdmin) return Forbid();
        if (string.IsNullOrWhiteSpace(dto.Code)) return BadRequest(ApiResult<Guid>.Fail("权限编码不能为空"));
        var code = dto.Code.Trim();
        if (await _db.SysPermissions.AnyAsync(p => p.Code == code, ct))
            return BadRequest(ApiResult<Guid>.Fail("权限编码已存在"));
        var perm = new SysPermission
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = (dto.Name ?? code).Trim(),
            Type = (PermissionType)(dto.Type ?? 0),
            ParentId = dto.ParentId,
            Path = dto.Path?.Trim(),
            Sort = dto.Sort ?? 0
        };
        _db.SysPermissions.Add(perm);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResult<Guid>.Ok(perm.Id));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResult<object>>> Update(Guid id, [FromBody] PermissionUpdateDto dto, CancellationToken ct = default)
    {
        if (!IsAdmin) return Forbid();
        var perm = await _db.SysPermissions.FindAsync(new object[] { id }, ct);
        if (perm == null) return NotFound();
        if (!string.IsNullOrWhiteSpace(dto.Name))
            perm.Name = dto.Name.Trim();
        if (dto.Type.HasValue)
            perm.Type = (PermissionType)dto.Type.Value;
        if (dto.ParentId.HasValue)
            perm.ParentId = dto.ParentId.Value;
        else if (dto.ClearParent == true)
            perm.ParentId = null;
        if (dto.Path != null)
            perm.Path = string.IsNullOrWhiteSpace(dto.Path) ? null : dto.Path.Trim();
        if (dto.Sort.HasValue)
            perm.Sort = dto.Sort.Value;
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResult<object>.Ok(new { }));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResult<object>>> Delete(Guid id, CancellationToken ct = default)
    {
        if (!IsAdmin) return Forbid();
        var perm = await _db.SysPermissions.FindAsync(new object[] { id }, ct);
        if (perm == null) return NotFound();
        var refCount = await _db.SysRolePermissions.CountAsync(rp => rp.PermissionId == id, ct);
        if (refCount > 0)
            return BadRequest(ApiResult<object>.Fail($"该权限已被 {refCount} 个角色引用，请先在角色权限设置中取消后再删除"));
        _db.SysPermissions.Remove(perm);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResult<object>.Ok(new { }));
    }
}

public class PermissionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public int Type { get; set; }
    public Guid? ParentId { get; set; }
    public string? Path { get; set; }
    public int Sort { get; set; }
}

public class PermissionCreateDto
{
    public string Code { get; set; } = null!;
    public string? Name { get; set; }
    public int? Type { get; set; }
    public Guid? ParentId { get; set; }
    public string? Path { get; set; }
    public int? Sort { get; set; }
}

public class PermissionUpdateDto
{
    public string? Name { get; set; }
    public int? Type { get; set; }
    public Guid? ParentId { get; set; }
    public bool? ClearParent { get; set; }
    public string? Path { get; set; }
    public int? Sort { get; set; }
}
