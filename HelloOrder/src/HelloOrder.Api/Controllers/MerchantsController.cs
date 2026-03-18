using System.Security.Claims;
using HelloOrder.Api.Common;
using HelloOrder.Application.Services;
using HelloOrder.Core.Entities;
using HelloOrder.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelloOrder.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MerchantsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IDataScopeService _dataScope;

    public MerchantsController(AppDbContext db, IDataScopeService dataScope)
    {
        _db = db;
        _dataScope = dataScope;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResult<PagedResult<MerchantDto>>>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? name = null,
        [FromQuery] string? platform = null,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        var role = GetRoleCode();
        var visibleIds = await _dataScope.GetVisibleBusinessUserIdsAsync(userId, role, ct);
        var q = _db.Merchants.AsNoTracking();
        if (visibleIds != null)
        {
            if (visibleIds.Count == 0) return Ok(ApiResult<PagedResult<MerchantDto>>.Ok(new PagedResult<MerchantDto> { List = new List<MerchantDto>(), Total = 0, Page = page, PageSize = pageSize }));
            q = q.Where(m => m.BusinessUserId != null && visibleIds.Contains(m.BusinessUserId.Value));
        }
        if (!string.IsNullOrWhiteSpace(name))
            q = q.Where(m => m.Name.Contains(name));
        if (!string.IsNullOrWhiteSpace(platform))
            q = q.Where(m => m.Platform == platform);
        var total = await q.CountAsync(ct);
        var list = await q.OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(m => new MerchantDto
            {
                Id = m.Id,
                Name = m.Name,
                Platform = m.Platform,
                ShopName = m.ShopName,
                Contact = m.Contact,
                SettlementType = m.SettlementType,
                BusinessUserId = m.BusinessUserId,
                Status = m.Status,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync(ct);
        return Ok(ApiResult<PagedResult<MerchantDto>>.Ok(new PagedResult<MerchantDto> { List = list, Total = total, Page = page, PageSize = pageSize }));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResult<MerchantDto>>> GetById(Guid id, CancellationToken ct)
    {
        var m = await _db.Merchants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (m == null) return NotFound();
        if (!await CanAccessMerchantAsync(m.BusinessUserId, ct)) return Forbid();
        return Ok(ApiResult<MerchantDto>.Ok(new MerchantDto
        {
            Id = m.Id,
            Name = m.Name,
            Platform = m.Platform,
            ShopName = m.ShopName,
            Contact = m.Contact,
            SettlementType = m.SettlementType,
            BusinessUserId = m.BusinessUserId,
            Status = m.Status,
            CreatedAt = m.CreatedAt,
            UpdatedAt = m.UpdatedAt
        }));
    }

    /// <summary>获取某商家下的店铺列表（用于下拉等）</summary>
    [HttpGet("{id}/shops")]
    public async Task<ActionResult<ApiResult<List<MerchantShopBriefDto>>>> GetShops(Guid id, CancellationToken ct = default)
    {
        var m = await _db.Merchants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (m == null) return NotFound();
        if (!await CanAccessMerchantAsync(m.BusinessUserId, ct)) return Forbid();
        var list = await _db.MerchantShops.AsNoTracking()
            .Where(s => s.MerchantId == id && s.Status == 1)
            .OrderBy(s => s.Name)
            .Select(s => new MerchantShopBriefDto { Id = s.Id, Name = s.Name, Platform = s.Platform })
            .ToListAsync(ct);
        return Ok(ApiResult<List<MerchantShopBriefDto>>.Ok(list));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResult<Guid>>> Post([FromBody] MerchantCreateDto dto, CancellationToken ct)
    {
        var role = GetRoleCode();
        if (role != "Admin" && role != "Business" && role != "Assistant")
            return Forbid();
        var businessUserId = role == "Business" ? GetUserId() : (role == "Assistant" ? (dto.BusinessUserId ?? await GetFirstLinkedBusinessUserIdAsync(ct)) : dto.BusinessUserId);
        if (businessUserId == null && role != "Admin") return BadRequest(ApiResult<Guid>.Fail("业务员未指定或助理未关联业务员"));
        var e = new Merchant
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Platform = dto.Platform,
            ShopName = dto.ShopName,
            Contact = dto.Contact,
            SettlementType = dto.SettlementType,
            BusinessUserId = businessUserId,
            Status = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Merchants.Add(e);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResult<Guid>.Ok(e.Id));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResult<object>>> Put(Guid id, [FromBody] MerchantUpdateDto dto, CancellationToken ct)
    {
        var e = await _db.Merchants.FindAsync(new object[] { id }, ct);
        if (e == null) return NotFound();
        if (!await CanAccessMerchantAsync(e.BusinessUserId, ct)) return Forbid();
        e.Name = dto.Name ?? e.Name;
        e.Platform = dto.Platform ?? e.Platform;
        e.ShopName = dto.ShopName ?? e.ShopName;
        e.Contact = dto.Contact ?? e.Contact;
        e.SettlementType = dto.SettlementType ?? e.SettlementType;
        e.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResult<object>.Ok(new { }));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResult<object>>> Delete(Guid id, CancellationToken ct)
    {
        var e = await _db.Merchants.FindAsync(new object[] { id }, ct);
        if (e == null) return NotFound();
        if (!await CanAccessMerchantAsync(e.BusinessUserId, ct)) return Forbid();
        e.Status = 0;
        e.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResult<object>.Ok(new { }));
    }

    private Guid? GetUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var uid) ? uid : null;
    }

    private string? GetRoleCode() => User.FindFirstValue("role_code") ?? "";

    private async Task<bool> CanAccessMerchantAsync(Guid? businessUserId, CancellationToken ct)
    {
        var role = GetRoleCode();
        if (role == "Admin" || role == "Boss") return true;
        var uid = GetUserId();
        if (businessUserId == uid) return true;
        if (role == "Assistant" && uid.HasValue && businessUserId.HasValue)
            return await _db.UserBusinessAssistants.AnyAsync(x => x.AssistantUserId == uid && x.BusinessUserId == businessUserId, ct);
        return false;
    }

    private async Task<Guid?> GetFirstLinkedBusinessUserIdAsync(CancellationToken ct)
    {
        var uid = GetUserId();
        if (!uid.HasValue) return null;
        var v = await _db.UserBusinessAssistants.AsNoTracking().Where(x => x.AssistantUserId == uid).Select(x => x.BusinessUserId).FirstOrDefaultAsync(ct);
        return v == default ? null : v;
    }
}

public class MerchantDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Platform { get; set; }
    public string? ShopName { get; set; }
    public string? Contact { get; set; }
    public string? SettlementType { get; set; }
    public Guid? BusinessUserId { get; set; }
    public int Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class MerchantCreateDto
{
    public string Name { get; set; } = null!;
    public string? Platform { get; set; }
    public string? ShopName { get; set; }
    public string? Contact { get; set; }
    public string? SettlementType { get; set; }
    public Guid? BusinessUserId { get; set; }
}

public class MerchantUpdateDto
{
    public string? Name { get; set; }
    public string? Platform { get; set; }
    public string? ShopName { get; set; }
    public string? Contact { get; set; }
    public string? SettlementType { get; set; }
}

public class MerchantShopBriefDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Platform { get; set; }
}
