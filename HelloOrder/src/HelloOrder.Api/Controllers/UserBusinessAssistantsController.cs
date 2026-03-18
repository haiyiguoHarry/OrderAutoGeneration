using System.Security.Claims;
using HelloOrder.Api.Common;
using HelloOrder.Core.Entities;
using HelloOrder.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelloOrder.Api.Controllers;

[ApiController]
[Route("api/user-business-assistants")]
[Authorize]
public class UserBusinessAssistantsController : ControllerBase
{
    private readonly AppDbContext _db;

    public UserBusinessAssistantsController(AppDbContext db) => _db = db;

    /// <summary>列表：仅 Admin。可按助理或业务员筛选。</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResult<List<UserBusinessAssistantDto>>>> Get(
        [FromQuery] Guid? assistantUserId = null,
        [FromQuery] Guid? businessUserId = null,
        CancellationToken ct = default)
    {
        if (GetRoleCode() != "Admin") return Forbid();
        IQueryable<UserBusinessAssistant> q = _db.UserBusinessAssistants.AsNoTracking()
            .Include(x => x.BusinessUser)
            .Include(x => x.AssistantUser);
        if (assistantUserId.HasValue)
            q = q.Where(x => x.AssistantUserId == assistantUserId.Value);
        if (businessUserId.HasValue)
            q = q.Where(x => x.BusinessUserId == businessUserId.Value);
        var list = await q.OrderBy(x => x.AssistantUserId).ThenBy(x => x.BusinessUserId)
            .Select(x => new UserBusinessAssistantDto
            {
                BusinessUserId = x.BusinessUserId,
                AssistantUserId = x.AssistantUserId,
                BusinessUserName = x.BusinessUser.RealName ?? x.BusinessUser.Username,
                AssistantUserName = x.AssistantUser.RealName ?? x.AssistantUser.Username,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(ct);
        return Ok(ApiResult<List<UserBusinessAssistantDto>>.Ok(list));
    }

    /// <summary>为助理关联业务员（Admin）</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResult<object>>> Post([FromBody] UserBusinessAssistantCreateDto dto, CancellationToken ct = default)
    {
        if (GetRoleCode() != "Admin") return Forbid();
        if (dto.AssistantUserId == dto.BusinessUserId)
            return BadRequest(ApiResult<object>.Fail("业务员与助理不能为同一用户"));
        var bus = await _db.SysUsers.FindAsync(new object[] { dto.BusinessUserId }, ct);
        var ast = await _db.SysUsers.FindAsync(new object[] { dto.AssistantUserId }, ct);
        if (bus == null || ast == null) return BadRequest(ApiResult<object>.Fail("用户不存在"));
        var exists = await _db.UserBusinessAssistants.AnyAsync(
            x => x.BusinessUserId == dto.BusinessUserId && x.AssistantUserId == dto.AssistantUserId, ct);
        if (exists) return BadRequest(ApiResult<object>.Fail("该关联已存在"));
        _db.UserBusinessAssistants.Add(new UserBusinessAssistant
        {
            BusinessUserId = dto.BusinessUserId,
            AssistantUserId = dto.AssistantUserId,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResult<object>.Ok(new { }));
    }

    /// <summary>批量设置某助理关联的业务员（Admin）：传入助理ID和业务员ID列表，先删后增</summary>
    [HttpPut("set-for-assistant")]
    public async Task<ActionResult<ApiResult<object>>> SetForAssistant([FromBody] SetBusinessUsersForAssistantDto dto, CancellationToken ct = default)
    {
        if (GetRoleCode() != "Admin") return Forbid();
        var existing = await _db.UserBusinessAssistants.Where(x => x.AssistantUserId == dto.AssistantUserId).ToListAsync(ct);
        _db.UserBusinessAssistants.RemoveRange(existing);
        var userIds = dto.BusinessUserIds?.Distinct().ToList() ?? new List<Guid>();
        foreach (var bid in userIds)
        {
            if (bid == dto.AssistantUserId) continue;
            if (!await _db.SysUsers.AnyAsync(u => u.Id == bid, ct)) continue;
            _db.UserBusinessAssistants.Add(new UserBusinessAssistant
            {
                BusinessUserId = bid,
                AssistantUserId = dto.AssistantUserId,
                CreatedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResult<object>.Ok(new { }));
    }

    /// <summary>解除关联（Admin）</summary>
    [HttpDelete]
    public async Task<ActionResult<ApiResult<object>>> Delete(
        [FromQuery] Guid assistantUserId,
        [FromQuery] Guid businessUserId,
        CancellationToken ct = default)
    {
        if (GetRoleCode() != "Admin") return Forbid();
        var e = await _db.UserBusinessAssistants
            .FirstOrDefaultAsync(x => x.AssistantUserId == assistantUserId && x.BusinessUserId == businessUserId, ct);
        if (e == null) return NotFound();
        _db.UserBusinessAssistants.Remove(e);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResult<object>.Ok(new { }));
    }

    private string? GetRoleCode() => User.FindFirstValue("role_code") ?? "";
}

public class UserBusinessAssistantDto
{
    public Guid BusinessUserId { get; set; }
    public Guid AssistantUserId { get; set; }
    public string? BusinessUserName { get; set; }
    public string? AssistantUserName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UserBusinessAssistantCreateDto
{
    public Guid BusinessUserId { get; set; }
    public Guid AssistantUserId { get; set; }
}

public class SetBusinessUsersForAssistantDto
{
    public Guid AssistantUserId { get; set; }
    public List<Guid>? BusinessUserIds { get; set; }
}
