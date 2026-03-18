using HelloOrder.Api.Common;
using HelloOrder.Core.Entities;
using HelloOrder.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelloOrder.Api.Controllers;

[ApiController]
[Route("api/express-country-rates")]
[Authorize]
public class ExpressCountryRatesController : ControllerBase
{
    private readonly AppDbContext _db;

    public ExpressCountryRatesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResult<PagedResult<ExpressCountryRateDto>>>> Get(
        [FromQuery] Guid? companyId = null,
        [FromQuery] string? countryCode = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        IQueryable<ExpressCountryRate> q = _db.ExpressCountryRates.AsNoTracking().Include(x => x.ExpressCompany);
        if (companyId.HasValue)
            q = q.Where(x => x.ExpressCompanyId == companyId.Value);
        if (!string.IsNullOrWhiteSpace(countryCode))
            q = q.Where(x => x.CountryCode.Contains(countryCode));
        var total = await q.CountAsync(ct);
        var list = await q.OrderBy(x => x.ExpressCompany!.Name).ThenBy(x => x.CountryCode)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new ExpressCountryRateDto
            {
                Id = r.Id,
                ExpressCompanyId = r.ExpressCompanyId,
                ExpressCompanyName = r.ExpressCompany!.Name,
                CountryCode = r.CountryCode,
                CountryName = r.CountryName,
                UnitPrice = r.UnitPrice,
                LeadDaysMin = r.LeadDaysMin,
                LeadDaysMax = r.LeadDaysMax,
                ChargeRule = r.ChargeRule,
                Remark = r.Remark,
                EffectiveFrom = r.EffectiveFrom,
                EffectiveTo = r.EffectiveTo
            })
            .ToListAsync(ct);
        return Ok(ApiResult<PagedResult<ExpressCountryRateDto>>.Ok(new PagedResult<ExpressCountryRateDto> { List = list, Total = total, Page = page, PageSize = pageSize }));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResult<Guid>>> Post([FromBody] ExpressCountryRateCreateDto dto, CancellationToken ct = default)
    {
        var exists = await _db.ExpressCompanies.AnyAsync(e => e.Id == dto.ExpressCompanyId, ct);
        if (!exists) return BadRequest(ApiResult<Guid>.Fail("快递公司不存在"));
        var e = new ExpressCountryRate
        {
            Id = Guid.NewGuid(),
            ExpressCompanyId = dto.ExpressCompanyId,
            CountryCode = dto.CountryCode,
            CountryName = dto.CountryName,
            UnitPrice = dto.UnitPrice,
            LeadDaysMin = dto.LeadDaysMin,
            LeadDaysMax = dto.LeadDaysMax,
            ChargeRule = dto.ChargeRule,
            Remark = dto.Remark,
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.ExpressCountryRates.Add(e);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResult<Guid>.Ok(e.Id));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResult<object>>> Put(Guid id, [FromBody] ExpressCountryRateUpdateDto dto, CancellationToken ct = default)
    {
        var e = await _db.ExpressCountryRates.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e == null) return NotFound();
        e.CountryCode = dto.CountryCode ?? e.CountryCode;
        e.CountryName = dto.CountryName ?? e.CountryName;
        e.UnitPrice = dto.UnitPrice ?? e.UnitPrice;
        e.LeadDaysMin = dto.LeadDaysMin ?? e.LeadDaysMin;
        e.LeadDaysMax = dto.LeadDaysMax ?? e.LeadDaysMax;
        e.ChargeRule = dto.ChargeRule ?? e.ChargeRule;
        e.Remark = dto.Remark ?? e.Remark;
        e.EffectiveFrom = dto.EffectiveFrom ?? e.EffectiveFrom;
        e.EffectiveTo = dto.EffectiveTo ?? e.EffectiveTo;
        e.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResult<object>.Ok(new { }));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResult<object>>> Delete(Guid id, CancellationToken ct = default)
    {
        var e = await _db.ExpressCountryRates.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e == null) return NotFound();
        _db.ExpressCountryRates.Remove(e);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResult<object>.Ok(new { }));
    }
}

public class ExpressCountryRateCreateDto
{
    public Guid ExpressCompanyId { get; set; }
    public string CountryCode { get; set; } = null!;
    public string? CountryName { get; set; }
    public decimal? UnitPrice { get; set; }
    public int? LeadDaysMin { get; set; }
    public int? LeadDaysMax { get; set; }
    public string? ChargeRule { get; set; }
    public string? Remark { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

public class ExpressCountryRateUpdateDto
{
    public string? CountryCode { get; set; }
    public string? CountryName { get; set; }
    public decimal? UnitPrice { get; set; }
    public int? LeadDaysMin { get; set; }
    public int? LeadDaysMax { get; set; }
    public string? ChargeRule { get; set; }
    public string? Remark { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}
