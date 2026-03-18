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
[Route("api/shops")]
[Authorize]
public class ShopsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IDataScopeService _dataScope;

    public ShopsController(AppDbContext db, IDataScopeService dataScope)
    {
        _db = db;
        _dataScope = dataScope;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResult<PagedResult<MerchantShopDto>>>> Get(
        [FromQuery] Guid? merchantId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? name = null,
        [FromQuery] string? platform = null,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        var role = GetRoleCode();
        var visibleIds = await _dataScope.GetVisibleBusinessUserIdsAsync(userId, role, ct);
        IQueryable<MerchantShop> q = _db.MerchantShops.AsNoTracking().Include(x => x.Merchant);
        if (visibleIds != null)
        {
            if (visibleIds.Count == 0) return Ok(ApiResult<PagedResult<MerchantShopDto>>.Ok(new PagedResult<MerchantShopDto> { List = new List<MerchantShopDto>(), Total = 0, Page = page, PageSize = pageSize }));
            q = q.Where(s => s.Merchant.BusinessUserId != null && visibleIds.Contains(s.Merchant.BusinessUserId.Value));
        }
        if (merchantId.HasValue)
            q = q.Where(s => s.MerchantId == merchantId.Value);
        if (!string.IsNullOrWhiteSpace(name))
            q = q.Where(s => s.Name.Contains(name));
        if (!string.IsNullOrWhiteSpace(platform))
            q = q.Where(s => s.Platform == platform);
        var total = await q.CountAsync(ct);
        var list = await q.OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new MerchantShopDto
            {
                Id = s.Id,
                MerchantId = s.MerchantId,
                MerchantName = s.Merchant.Name,
                Name = s.Name,
                Platform = s.Platform,
                ShopUrl = s.ShopUrl,
                Remark = s.Remark,
                Status = s.Status,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync(ct);
        return Ok(ApiResult<PagedResult<MerchantShopDto>>.Ok(new PagedResult<MerchantShopDto> { List = list, Total = total, Page = page, PageSize = pageSize }));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResult<MerchantShopDto>>> GetById(Guid id, CancellationToken ct = default)
    {
        var s = await _db.MerchantShops.AsNoTracking().Include(x => x.Merchant).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s == null) return NotFound();
        if (!await CanAccessMerchantAsync(s.Merchant.BusinessUserId, ct)) return Forbid();
        return Ok(ApiResult<MerchantShopDto>.Ok(new MerchantShopDto
        {
            Id = s.Id,
            MerchantId = s.MerchantId,
            MerchantName = s.Merchant.Name,
            Name = s.Name,
            Platform = s.Platform,
            ShopUrl = s.ShopUrl,
            Remark = s.Remark,
            Status = s.Status,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt
        }));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResult<Guid>>> Post([FromBody] MerchantShopCreateDto dto, CancellationToken ct = default)
    {
        var merchant = await _db.Merchants.FindAsync(new object[] { dto.MerchantId }, ct);
        if (merchant == null) return BadRequest(ApiResult<Guid>.Fail("商家不存在"));
        if (!await CanAccessMerchantAsync(merchant.BusinessUserId, ct)) return Forbid();
        var e = new MerchantShop
        {
            Id = Guid.NewGuid(),
            MerchantId = dto.MerchantId,
            Name = dto.Name,
            Platform = dto.Platform,
            ShopUrl = dto.ShopUrl,
            Remark = dto.Remark,
            Status = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.MerchantShops.Add(e);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResult<Guid>.Ok(e.Id));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResult<object>>> Put(Guid id, [FromBody] MerchantShopUpdateDto dto, CancellationToken ct = default)
    {
        var e = await _db.MerchantShops.Include(x => x.Merchant).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e == null) return NotFound();
        if (!await CanAccessMerchantAsync(e.Merchant.BusinessUserId, ct)) return Forbid();
        e.Name = dto.Name ?? e.Name;
        e.Platform = dto.Platform ?? e.Platform;
        e.ShopUrl = dto.ShopUrl ?? e.ShopUrl;
        e.Remark = dto.Remark ?? e.Remark;
        e.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResult<object>.Ok(new { }));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResult<object>>> Delete(Guid id, CancellationToken ct = default)
    {
        var e = await _db.MerchantShops.Include(x => x.Merchant).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e == null) return NotFound();
        if (!await CanAccessMerchantAsync(e.Merchant.BusinessUserId, ct)) return Forbid();
        e.Status = 0;
        e.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResult<object>.Ok(new { }));
    }

    private Guid? GetUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : null;
    private string? GetRoleCode() => User.FindFirstValue("role_code") ?? "";
    private async Task<bool> CanAccessMerchantAsync(Guid? businessUserId, CancellationToken ct)
    {
        if (GetRoleCode() is "Admin" or "Boss") return true;
        var uid = GetUserId();
        if (businessUserId == uid) return true;
        if (GetRoleCode() == "Assistant" && uid.HasValue && businessUserId.HasValue)
            return await _db.UserBusinessAssistants.AnyAsync(x => x.AssistantUserId == uid && x.BusinessUserId == businessUserId, ct);
        return false;
    }
}

public class MerchantShopDto
{
    public Guid Id { get; set; }
    public Guid MerchantId { get; set; }
    public string? MerchantName { get; set; }
    public string Name { get; set; } = null!;
    public string? Platform { get; set; }
    public string? ShopUrl { get; set; }
    public string? Remark { get; set; }
    public int Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class MerchantShopCreateDto
{
    public Guid MerchantId { get; set; }
    public string Name { get; set; } = null!;
    public string? Platform { get; set; }
    public string? ShopUrl { get; set; }
    public string? Remark { get; set; }
}

public class MerchantShopUpdateDto
{
    public string? Name { get; set; }
    public string? Platform { get; set; }
    public string? ShopUrl { get; set; }
    public string? Remark { get; set; }
}
