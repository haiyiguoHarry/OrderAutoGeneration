using System.Security.Claims;
using HelloOrder.Api.Common;
using HelloOrder.Application.Services;
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
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IDataScopeService _dataScope;

    public OrdersController(AppDbContext db, IDataScopeService dataScope)
    {
        _db = db;
        _dataScope = dataScope;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResult<PagedResult<OrderListDto>>>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? merchantId = null,
        [FromQuery] Guid? merchantShopId = null,
        [FromQuery] OrderStatus? status = null,
        [FromQuery] Guid? conversionJobId = null,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        var role = GetRoleCode();
        var visibleIds = await _dataScope.GetVisibleBusinessUserIdsAsync(userId, role, ct);
        IQueryable<Order> q = _db.Orders.AsNoTracking().Include(o => o.Merchant).Include(o => o.MerchantShop);
        if (visibleIds != null)
        {
            if (visibleIds.Count == 0) return Ok(ApiResult<PagedResult<OrderListDto>>.Ok(new PagedResult<OrderListDto> { List = new List<OrderListDto>(), Total = 0, Page = page, PageSize = pageSize }));
            q = q.Where(o => o.BusinessUserId != null && visibleIds.Contains(o.BusinessUserId.Value));
        }
        if (merchantId.HasValue)
            q = q.Where(o => o.MerchantId == merchantId.Value);
        if (merchantShopId.HasValue)
            q = q.Where(o => o.MerchantShopId == merchantShopId.Value);
        if (status.HasValue)
            q = q.Where(o => o.Status == status.Value);
        if (conversionJobId.HasValue)
            q = q.Where(o => o.ConversionJobId == conversionJobId.Value);
        var total = await q.CountAsync(ct);
        var list = await q.OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new OrderListDto
            {
                Id = o.Id,
                MerchantId = o.MerchantId,
                MerchantName = o.Merchant.Name,
                MerchantShopId = o.MerchantShopId,
                MerchantShopName = o.MerchantShop != null ? o.MerchantShop.Name : null,
                OrderNo = o.OrderNo,
                Status = o.Status,
                TotalAmount = o.TotalAmount,
                Currency = o.Currency,
                OrderTime = o.OrderTime,
                CreatedAt = o.CreatedAt
            })
            .ToListAsync(ct);
        return Ok(ApiResult<PagedResult<OrderListDto>>.Ok(new PagedResult<OrderListDto> { List = list, Total = total, Page = page, PageSize = pageSize }));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResult<OrderDetailDto>>> GetById(Guid id, CancellationToken ct)
    {
        var o = await _db.Orders.AsNoTracking().Include(x => x.Merchant).Include(x => x.MerchantShop).Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (o == null) return NotFound();
        if (!await CanAccessOrderAsync(o.BusinessUserId, o.AssistantUserId, ct)) return Forbid();
        var dto = new OrderDetailDto
        {
            Id = o.Id,
            MerchantId = o.MerchantId,
            MerchantName = o.Merchant.Name,
            MerchantShopId = o.MerchantShopId,
            MerchantShopName = o.MerchantShop != null ? o.MerchantShop.Name : null,
            OrderNo = o.OrderNo,
            PlatformOrderId = o.PlatformOrderId,
            BusinessUserId = o.BusinessUserId,
            AssistantUserId = o.AssistantUserId,
            LastOperatorId = o.LastOperatorId,
            Status = o.Status,
            TotalAmount = o.TotalAmount,
            Currency = o.Currency,
            OrderTime = o.OrderTime,
            PaidAt = o.PaidAt,
            CreatedAt = o.CreatedAt,
            Items = o.Items.Select(i => new OrderItemDto { Id = i.Id, Sku = i.Sku, ProductName = i.ProductName, Spec = i.Spec, Quantity = i.Quantity, Price = i.Price, Link1688 = i.Link1688, PurchasePrice = i.PurchasePrice, Remark = i.Remark }).ToList()
        };
        return Ok(ApiResult<OrderDetailDto>.Ok(dto));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResult<Guid>>> Post([FromBody] OrderCreateDto dto, CancellationToken ct)
    {
        var merchant = await _db.Merchants.FindAsync(new object[] { dto.MerchantId }, ct);
        if (merchant == null) return BadRequest(ApiResult<Guid>.Fail("商家不存在"));
        if (!await CanAccessMerchantAsync(merchant.BusinessUserId, ct)) return Forbid();
        if (dto.MerchantShopId.HasValue)
        {
            var shop = await _db.MerchantShops.FirstOrDefaultAsync(s => s.Id == dto.MerchantShopId.Value && s.MerchantId == dto.MerchantId, ct);
            if (shop == null) return BadRequest(ApiResult<Guid>.Fail("店铺不存在或不属于该商家"));
        }
        var order = new Order
        {
            Id = Guid.NewGuid(),
            MerchantId = dto.MerchantId,
            MerchantShopId = dto.MerchantShopId,
            OrderNo = dto.OrderNo ?? $"ORD{DateTime.UtcNow:yyyyMMddHHmmss}",
            PlatformOrderId = dto.PlatformOrderId,
            BusinessUserId = merchant.BusinessUserId ?? GetUserId(),
            AssistantUserId = dto.AssistantUserId,
            LastOperatorId = GetUserId(),
            Status = OrderStatus.PendingQuote,
            TotalAmount = dto.TotalAmount,
            Currency = dto.Currency ?? "CNY",
            OrderTime = dto.OrderTime,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Orders.Add(order);
        foreach (var item in dto.Items)
        {
            _db.OrderItems.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Sku = item.Sku,
                ProductName = item.ProductName,
                Spec = item.Spec,
                Quantity = item.Quantity,
                Price = item.Price,
                Link1688 = item.Link1688,
                PurchasePrice = item.PurchasePrice,
                Remark = item.Remark
            });
        }
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResult<Guid>.Ok(order.Id));
    }

    [HttpPut("{id}/status")]
    public async Task<ActionResult<ApiResult<object>>> UpdateStatus(Guid id, [FromBody] UpdateOrderStatusDto dto, CancellationToken ct)
    {
        var o = await _db.Orders.FindAsync(new object[] { id }, ct);
        if (o == null) return NotFound();
        if (!await CanAccessOrderAsync(o.BusinessUserId, o.AssistantUserId, ct)) return Forbid();
        o.Status = dto.Status;
        o.LastOperatorId = GetUserId();
        if (dto.Status == OrderStatus.Paid && !o.PaidAt.HasValue)
            o.PaidAt = DateTime.UtcNow;
        o.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResult<object>.Ok(new { }));
    }

    private Guid? GetUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var uid) ? uid : null;
    }

    private string? GetRoleCode() => User.FindFirstValue("role_code") ?? "";

    private async Task<bool> CanAccessOrderAsync(Guid? businessUserId, Guid? assistantUserId, CancellationToken ct)
    {
        if (GetRoleCode() is "Admin" or "Boss") return true;
        var uid = GetUserId();
        if (businessUserId == uid || assistantUserId == uid) return true;
        if (GetRoleCode() == "Assistant" && uid.HasValue && businessUserId.HasValue)
            return await _db.UserBusinessAssistants.AnyAsync(x => x.AssistantUserId == uid && x.BusinessUserId == businessUserId, ct);
        return false;
    }

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

public class OrderListDto
{
    public Guid Id { get; set; }
    public Guid MerchantId { get; set; }
    public string? MerchantName { get; set; }
    public Guid? MerchantShopId { get; set; }
    public string? MerchantShopName { get; set; }
    public string OrderNo { get; set; } = null!;
    public OrderStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = null!;
    public DateTime? OrderTime { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class OrderDetailDto : OrderListDto
{
    public string? PlatformOrderId { get; set; }
    public Guid? BusinessUserId { get; set; }
    public Guid? AssistantUserId { get; set; }
    public Guid? LastOperatorId { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
}

public class OrderItemDto
{
    public Guid Id { get; set; }
    public string? Sku { get; set; }
    public string? ProductName { get; set; }
    public string? Spec { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public string? Link1688 { get; set; }
    public decimal? PurchasePrice { get; set; }
    public string? Remark { get; set; }
}

public class OrderCreateDto
{
    public Guid MerchantId { get; set; }
    public Guid? MerchantShopId { get; set; }
    public string? OrderNo { get; set; }
    public string? PlatformOrderId { get; set; }
    public Guid? AssistantUserId { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Currency { get; set; }
    public DateTime? OrderTime { get; set; }
    public List<OrderItemCreateDto> Items { get; set; } = new();
}

public class OrderItemCreateDto
{
    public string? Sku { get; set; }
    public string? ProductName { get; set; }
    public string? Spec { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public string? Link1688 { get; set; }
    public decimal? PurchasePrice { get; set; }
    public string? Remark { get; set; }
}

public class UpdateOrderStatusDto
{
    public OrderStatus Status { get; set; }
}
