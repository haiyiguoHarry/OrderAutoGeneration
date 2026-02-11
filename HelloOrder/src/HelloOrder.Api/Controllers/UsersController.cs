using System.Security.Claims;
using HelloOrder.Api.Common;
using HelloOrder.Core.Entities;
using HelloOrder.Infrastructure;
using HelloOrder.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelloOrder.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<ApiResult<PagedResult<UserDto>>>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? username = null,
        [FromQuery] string? realName = null,
        [FromQuery] Guid? roleId = null,
        [FromQuery] int? status = null,
        CancellationToken ct = default)
    {
        if (GetRoleCode() != "Admin") return Forbid();
        var q = _db.SysUsers.AsNoTracking().Include(u => u.Role);
        if (!string.IsNullOrWhiteSpace(username))
            q = q.Where(u => u.Username.Contains(username));
        if (!string.IsNullOrWhiteSpace(realName))
            q = q.Where(u => u.RealName.Contains(realName));
        if (roleId.HasValue)
            q = q.Where(u => u.RoleId == roleId.Value);
        if (status.HasValue)
            q = q.Where(u => u.Status == status.Value);
        else
            q = q.Where(u => u.Status == 1); // 默认只显示正常用户
        var total = await q.CountAsync(ct);
        var list = await q.OrderBy(u => u.Username)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new UserDto
            {
                Id = u.Id,
                Username = u.Username,
                RealName = u.RealName,
                RoleId = u.RoleId,
                RoleName = u.Role != null ? u.Role.Name : null,
                Status = u.Status,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt
            })
            .ToListAsync(ct);
        return Ok(ApiResult<PagedResult<UserDto>>.Ok(new PagedResult<UserDto> { List = list, Total = total, Page = page, PageSize = pageSize }));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResult<UserDto>>> GetById(Guid id, CancellationToken ct = default)
    {
        if (GetRoleCode() != "Admin") return Forbid();
        var u = await _db.SysUsers.AsNoTracking().Include(x => x.Role).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (u == null) return NotFound();
        return Ok(ApiResult<UserDto>.Ok(new UserDto
        {
            Id = u.Id,
            Username = u.Username,
            RealName = u.RealName,
            RoleId = u.RoleId,
            RoleName = u.Role != null ? u.Role.Name : null,
            Status = u.Status,
            CreatedAt = u.CreatedAt,
            UpdatedAt = u.UpdatedAt
        }));
    }

    [HttpGet("roles")]
    public async Task<ActionResult<ApiResult<List<RoleDto>>>> GetRoles(CancellationToken ct = default)
    {
        if (GetRoleCode() != "Admin") return Forbid();
        var list = await _db.SysRoles.AsNoTracking()
            .Select(r => new RoleDto { Id = r.Id, Code = r.Code, Name = r.Name })
            .ToListAsync(ct);
        return Ok(ApiResult<List<RoleDto>>.Ok(list));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResult<Guid>>> Create([FromBody] UserCreateDto dto, CancellationToken ct = default)
    {
        if (GetRoleCode() != "Admin") return Forbid();
        if (string.IsNullOrWhiteSpace(dto.Username)) return BadRequest(ApiResult<Guid>.Fail("用户名不能为空"));
        if (await _db.SysUsers.AnyAsync(u => u.Username == dto.Username.Trim(), ct))
            return BadRequest(ApiResult<Guid>.Fail("用户名已存在"));
        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
            return BadRequest(ApiResult<Guid>.Fail("密码至少 6 位"));
        if (dto.RoleId.HasValue && !await _db.SysRoles.AnyAsync(r => r.Id == dto.RoleId.Value, ct))
            return BadRequest(ApiResult<Guid>.Fail("角色不存在"));
        var user = new SysUser
        {
            Id = Guid.NewGuid(),
            Username = dto.Username.Trim(),
            PasswordHash = AuthService.HashPasswordForCreate(dto.Password),
            RealName = (dto.RealName ?? "").Trim(),
            RoleId = dto.RoleId,
            Status = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.SysUsers.Add(user);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResult<Guid>.Ok(user.Id));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResult<object>>> Update(Guid id, [FromBody] UserUpdateDto dto, CancellationToken ct = default)
    {
        if (GetRoleCode() != "Admin") return Forbid();
        var user = await _db.SysUsers.FindAsync(new object[] { id }, ct);
        if (user == null) return NotFound();
        if (!string.IsNullOrWhiteSpace(dto.RealName))
            user.RealName = dto.RealName.Trim();
        if (dto.RoleId.HasValue)
        {
            if (!await _db.SysRoles.AnyAsync(r => r.Id == dto.RoleId.Value, ct))
                return BadRequest(ApiResult<object>.Fail("角色不存在"));
            user.RoleId = dto.RoleId.Value;
        }
        if (dto.Status.HasValue)
            user.Status = dto.Status.Value;
        if (!string.IsNullOrWhiteSpace(dto.NewPassword))
        {
            if (dto.NewPassword.Length < 6)
                return BadRequest(ApiResult<object>.Fail("新密码至少 6 位"));
            user.PasswordHash = AuthService.HashPasswordForCreate(dto.NewPassword);
        }
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResult<object>.Ok(new { }));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResult<object>>> Delete(Guid id, CancellationToken ct = default)
    {
        if (GetRoleCode() != "Admin") return Forbid();
        var currentUserId = GetUserId();
        if (currentUserId == id)
            return BadRequest(ApiResult<object>.Fail("不能删除当前登录用户"));
        var user = await _db.SysUsers.FindAsync(new object[] { id }, ct);
        if (user == null) return NotFound();
        user.Status = 0;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResult<object>.Ok(new { }));
    }

    private Guid? GetUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var uid) ? uid : null;
    }

    private string? GetRoleCode() => User.FindFirstValue("role_code") ?? "";
}

public class UserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = null!;
    public string RealName { get; set; } = null!;
    public Guid? RoleId { get; set; }
    public string? RoleName { get; set; }
    public int Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class UserCreateDto
{
    public string Username { get; set; } = null!;
    public string? RealName { get; set; }
    public string Password { get; set; } = null!;
    public Guid? RoleId { get; set; }
}

public class UserUpdateDto
{
    public string? RealName { get; set; }
    public Guid? RoleId { get; set; }
    public int? Status { get; set; }
    public string? NewPassword { get; set; }
}

public class RoleDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
}
