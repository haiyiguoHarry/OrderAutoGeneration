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
[Route("api/products")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IDataScopeService _dataScope;

    public ProductsController(AppDbContext db, IDataScopeService dataScope)
    {
        _db = db;
        _dataScope = dataScope;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResult<PagedResult<ProductDto>>>> Get(
        [FromQuery] Guid? shopId = null,
        [FromQuery] Guid? merchantId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sku = null,
        [FromQuery] string? name = null,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        var role = GetRoleCode();
        var visibleIds = await _dataScope.GetVisibleBusinessUserIdsAsync(userId, role, ct);
        IQueryable<Product> q = _db.Products.AsNoTracking().Include(x => x.MerchantShop).ThenInclude(s => s.Merchant);
        if (visibleIds != null)
        {
            if (visibleIds.Count == 0) return Ok(ApiResult<PagedResult<ProductDto>>.Ok(new PagedResult<ProductDto> { List = new List<ProductDto>(), Total = 0, Page = page, PageSize = pageSize }));
            q = q.Where(p => p.MerchantShop.Merchant.BusinessUserId != null && visibleIds.Contains(p.MerchantShop.Merchant.BusinessUserId.Value));
        }
        if (shopId.HasValue)
            q = q.Where(p => p.MerchantShopId == shopId.Value);
        if (merchantId.HasValue)
            q = q.Where(p => p.MerchantShop.MerchantId == merchantId.Value);
        if (!string.IsNullOrWhiteSpace(sku))
            q = q.Where(p => p.Sku != null && p.Sku.Contains(sku));
        if (!string.IsNullOrWhiteSpace(name))
            q = q.Where(p => p.Name.Contains(name));
        var total = await q.CountAsync(ct);
        var list = await q.OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                MerchantShopId = p.MerchantShopId,
                ShopName = p.MerchantShop.Name,
                Sku = p.Sku,
                Name = p.Name,
                NameEn = p.NameEn,
                Spec = p.Spec,
                ImageUrl = p.ImageUrl,
                PlatformUrl = p.PlatformUrl,
                WeightKg = p.WeightKg,
                WeightGrams = p.WeightGrams,
                LengthCm = p.LengthCm,
                WidthCm = p.WidthCm,
                HeightCm = p.HeightCm,
                Link1688 = p.Link1688,
                CustomerLink = p.CustomerLink,
                FactoryLink = p.FactoryLink,
                Material = p.Material,
                StyleName = p.StyleName,
                SizeChart = p.SizeChart,
                PackageNote = p.PackageNote,
                SuggestedPurchasePrice = p.SuggestedPurchasePrice,
                SuggestedSalePrice = p.SuggestedSalePrice,
                Remark = p.Remark,
                Status = p.Status,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync(ct);
        return Ok(ApiResult<PagedResult<ProductDto>>.Ok(new PagedResult<ProductDto> { List = list, Total = total, Page = page, PageSize = pageSize }));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResult<ProductDto>>> GetById(Guid id, CancellationToken ct = default)
    {
        var p = await _db.Products.AsNoTracking().Include(x => x.MerchantShop).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p == null) return NotFound();
        if (!await CanAccessShopAsync(p.MerchantShopId, ct)) return Forbid();
        return Ok(ApiResult<ProductDto>.Ok(new ProductDto
        {
            Id = p.Id,
            MerchantShopId = p.MerchantShopId,
            ShopName = p.MerchantShop.Name,
            Sku = p.Sku,
            Name = p.Name,
            NameEn = p.NameEn,
            Spec = p.Spec,
            ImageUrl = p.ImageUrl,
            PlatformUrl = p.PlatformUrl,
            WeightKg = p.WeightKg,
            WeightGrams = p.WeightGrams,
            LengthCm = p.LengthCm,
            WidthCm = p.WidthCm,
            HeightCm = p.HeightCm,
            Link1688 = p.Link1688,
            CustomerLink = p.CustomerLink,
            FactoryLink = p.FactoryLink,
            Material = p.Material,
            StyleName = p.StyleName,
            SizeChart = p.SizeChart,
            PackageNote = p.PackageNote,
            SuggestedPurchasePrice = p.SuggestedPurchasePrice,
            SuggestedSalePrice = p.SuggestedSalePrice,
            Remark = p.Remark,
            Status = p.Status,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        }));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResult<Guid>>> Post([FromBody] ProductCreateDto dto, CancellationToken ct = default)
    {
        var shop = await _db.MerchantShops.Include(s => s.Merchant).FirstOrDefaultAsync(s => s.Id == dto.MerchantShopId, ct);
        if (shop == null) return BadRequest(ApiResult<Guid>.Fail("店铺不存在"));
        if (!await CanAccessMerchantAsync(shop.Merchant.BusinessUserId, ct)) return Forbid();
        var e = new Product
        {
            Id = Guid.NewGuid(),
            MerchantShopId = dto.MerchantShopId,
            Sku = dto.Sku,
            Name = dto.Name,
            NameEn = dto.NameEn,
            Spec = dto.Spec,
            ImageUrl = dto.ImageUrl,
            PlatformUrl = dto.PlatformUrl,
            WeightKg = dto.WeightKg,
            WeightGrams = dto.WeightGrams,
            LengthCm = dto.LengthCm,
            WidthCm = dto.WidthCm,
            HeightCm = dto.HeightCm,
            Link1688 = dto.Link1688,
            CustomerLink = dto.CustomerLink,
            FactoryLink = dto.FactoryLink,
            Material = dto.Material,
            StyleName = dto.StyleName,
            SizeChart = dto.SizeChart,
            PackageNote = dto.PackageNote,
            SuggestedPurchasePrice = dto.SuggestedPurchasePrice,
            SuggestedSalePrice = dto.SuggestedSalePrice,
            Remark = dto.Remark,
            Status = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Products.Add(e);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResult<Guid>.Ok(e.Id));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResult<object>>> Put(Guid id, [FromBody] ProductUpdateDto dto, CancellationToken ct = default)
    {
        var e = await _db.Products.Include(x => x.MerchantShop).ThenInclude(s => s.Merchant).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e == null) return NotFound();
        if (!await CanAccessMerchantAsync(e.MerchantShop.Merchant.BusinessUserId, ct)) return Forbid();
        e.Sku = dto.Sku ?? e.Sku;
        e.Name = dto.Name ?? e.Name;
        e.NameEn = dto.NameEn ?? e.NameEn;
        e.Spec = dto.Spec ?? e.Spec;
        e.ImageUrl = dto.ImageUrl ?? e.ImageUrl;
        e.PlatformUrl = dto.PlatformUrl ?? e.PlatformUrl;
        e.WeightKg = dto.WeightKg ?? e.WeightKg;
        e.WeightGrams = dto.WeightGrams ?? e.WeightGrams;
        e.LengthCm = dto.LengthCm ?? e.LengthCm;
        e.WidthCm = dto.WidthCm ?? e.WidthCm;
        e.HeightCm = dto.HeightCm ?? e.HeightCm;
        e.Link1688 = dto.Link1688 ?? e.Link1688;
        e.CustomerLink = dto.CustomerLink ?? e.CustomerLink;
        e.FactoryLink = dto.FactoryLink ?? e.FactoryLink;
        e.Material = dto.Material ?? e.Material;
        e.StyleName = dto.StyleName ?? e.StyleName;
        e.SizeChart = dto.SizeChart ?? e.SizeChart;
        e.PackageNote = dto.PackageNote ?? e.PackageNote;
        e.SuggestedPurchasePrice = dto.SuggestedPurchasePrice ?? e.SuggestedPurchasePrice;
        e.SuggestedSalePrice = dto.SuggestedSalePrice ?? e.SuggestedSalePrice;
        e.Remark = dto.Remark ?? e.Remark;
        e.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResult<object>.Ok(new { }));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResult<object>>> Delete(Guid id, CancellationToken ct = default)
    {
        var e = await _db.Products.Include(x => x.MerchantShop).ThenInclude(s => s.Merchant).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e == null) return NotFound();
        if (!await CanAccessMerchantAsync(e.MerchantShop.Merchant.BusinessUserId, ct)) return Forbid();
        e.Status = 0;
        e.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResult<object>.Ok(new { }));
    }

    private Guid? GetUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : null;
    private string? GetRoleCode() => User.FindFirstValue("role_code") ?? "";
    private async Task<bool> CanAccessShopAsync(Guid shopId, CancellationToken ct)
    {
        var shop = await _db.MerchantShops.AsNoTracking().Include(s => s.Merchant).FirstOrDefaultAsync(s => s.Id == shopId, ct);
        return shop != null && await CanAccessMerchantAsync(shop.Merchant.BusinessUserId, ct);
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

public class ProductDto
{
    public Guid Id { get; set; }
    public Guid MerchantShopId { get; set; }
    public string? ShopName { get; set; }
    public string? Sku { get; set; }
    public string Name { get; set; } = null!;
    public string? NameEn { get; set; }
    public string? Spec { get; set; }
    public string? ImageUrl { get; set; }
    public string? PlatformUrl { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? WeightGrams { get; set; }
    public decimal? LengthCm { get; set; }
    public decimal? WidthCm { get; set; }
    public decimal? HeightCm { get; set; }
    public string? Link1688 { get; set; }
    public string? CustomerLink { get; set; }
    public string? FactoryLink { get; set; }
    public string? Material { get; set; }
    public string? StyleName { get; set; }
    public string? SizeChart { get; set; }
    public string? PackageNote { get; set; }
    public decimal? SuggestedPurchasePrice { get; set; }
    public decimal? SuggestedSalePrice { get; set; }
    public string? Remark { get; set; }
    public int Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class ProductCreateDto
{
    public Guid MerchantShopId { get; set; }
    public string? Sku { get; set; }
    public string Name { get; set; } = null!;
    public string? NameEn { get; set; }
    public string? Spec { get; set; }
    public string? ImageUrl { get; set; }
    public string? PlatformUrl { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? WeightGrams { get; set; }
    public decimal? LengthCm { get; set; }
    public decimal? WidthCm { get; set; }
    public decimal? HeightCm { get; set; }
    public string? Link1688 { get; set; }
    public string? CustomerLink { get; set; }
    public string? FactoryLink { get; set; }
    public string? Material { get; set; }
    public string? StyleName { get; set; }
    public string? SizeChart { get; set; }
    public string? PackageNote { get; set; }
    public decimal? SuggestedPurchasePrice { get; set; }
    public decimal? SuggestedSalePrice { get; set; }
    public string? Remark { get; set; }
}

public class ProductUpdateDto
{
    public string? Sku { get; set; }
    public string? Name { get; set; }
    public string? NameEn { get; set; }
    public string? Spec { get; set; }
    public string? ImageUrl { get; set; }
    public string? PlatformUrl { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? WeightGrams { get; set; }
    public decimal? LengthCm { get; set; }
    public decimal? WidthCm { get; set; }
    public decimal? HeightCm { get; set; }
    public string? Link1688 { get; set; }
    public string? CustomerLink { get; set; }
    public string? FactoryLink { get; set; }
    public string? Material { get; set; }
    public string? StyleName { get; set; }
    public string? SizeChart { get; set; }
    public string? PackageNote { get; set; }
    public decimal? SuggestedPurchasePrice { get; set; }
    public decimal? SuggestedSalePrice { get; set; }
    public string? Remark { get; set; }
}
