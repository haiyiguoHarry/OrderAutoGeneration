using HelloOrder.Api.Common;
using HelloOrder.Core.Entities;
using HelloOrder.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelloOrder.Api.Controllers;

[ApiController]
[Route("api/express-companies")]
[Authorize]
public class ExpressCompaniesController : ControllerBase
{
    private readonly AppDbContext _db;

    public ExpressCompaniesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResult<PagedResult<ExpressCompanyDto>>>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? name = null,
        [FromQuery] int? status = null,
        CancellationToken ct = default)
    {
        IQueryable<ExpressCompany> q = _db.ExpressCompanies.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(name))
            q = q.Where(x => x.Name.Contains(name) || (x.Code != null && x.Code.Contains(name)));
        if (status.HasValue)
            q = q.Where(x => x.Status == status.Value);
        var total = await q.CountAsync(ct);
        var list = await q.OrderBy(x => x.Name)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ExpressCompanyDto
            {
                Id = x.Id,
                Code = x.Code,
                Name = x.Name,
                Contact = x.Contact,
                Remark = x.Remark,
                Status = x.Status,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync(ct);
        return Ok(ApiResult<PagedResult<ExpressCompanyDto>>.Ok(new PagedResult<ExpressCompanyDto> { List = list, Total = total, Page = page, PageSize = pageSize }));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResult<ExpressCompanyDto>>> GetById(Guid id, CancellationToken ct = default)
    {
        var x = await _db.ExpressCompanies.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
        if (x == null) return NotFound();
        return Ok(ApiResult<ExpressCompanyDto>.Ok(new ExpressCompanyDto
        {
            Id = x.Id,
            Code = x.Code,
            Name = x.Name,
            Contact = x.Contact,
            Remark = x.Remark,
            Status = x.Status,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt
        }));
    }

    [HttpGet("{id}/rates")]
    public async Task<ActionResult<ApiResult<List<ExpressCountryRateDto>>>> GetRates(Guid id, [FromQuery] string? countryCode = null, CancellationToken ct = default)
    {
        var exists = await _db.ExpressCompanies.AnyAsync(e => e.Id == id, ct);
        if (!exists) return NotFound();
        var q = _db.ExpressCountryRates.AsNoTracking().Where(r => r.ExpressCompanyId == id);
        if (!string.IsNullOrWhiteSpace(countryCode))
            q = q.Where(r => r.CountryCode == countryCode);
        var list = await q.OrderBy(r => r.CountryCode)
            .Select(r => new ExpressCountryRateDto
            {
                Id = r.Id,
                ExpressCompanyId = r.ExpressCompanyId,
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
        return Ok(ApiResult<List<ExpressCountryRateDto>>.Ok(list));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResult<Guid>>> Post([FromBody] ExpressCompanyCreateDto dto, CancellationToken ct = default)
    {
        var e = new ExpressCompany
        {
            Id = Guid.NewGuid(),
            Code = dto.Code,
            Name = dto.Name,
            Contact = dto.Contact,
            Remark = dto.Remark,
            Status = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.ExpressCompanies.Add(e);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResult<Guid>.Ok(e.Id));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResult<object>>> Put(Guid id, [FromBody] ExpressCompanyUpdateDto dto, CancellationToken ct = default)
    {
        var e = await _db.ExpressCompanies.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e == null) return NotFound();
        e.Code = dto.Code ?? e.Code;
        e.Name = dto.Name ?? e.Name;
        e.Contact = dto.Contact ?? e.Contact;
        e.Remark = dto.Remark ?? e.Remark;
        if (dto.Status.HasValue) e.Status = dto.Status.Value;
        e.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResult<object>.Ok(new { }));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResult<object>>> Delete(Guid id, CancellationToken ct = default)
    {
        var e = await _db.ExpressCompanies.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e == null) return NotFound();
        e.Status = 0;
        e.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResult<object>.Ok(new { }));
    }
}

public class ExpressCompanyDto
{
    public Guid Id { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = null!;
    public string? Contact { get; set; }
    public string? Remark { get; set; }
    public int Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ExpressCompanyCreateDto
{
    public string? Code { get; set; }
    public string Name { get; set; } = null!;
    public string? Contact { get; set; }
    public string? Remark { get; set; }
}

public class ExpressCompanyUpdateDto
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Contact { get; set; }
    public string? Remark { get; set; }
    public int? Status { get; set; }
}

public class ExpressCountryRateDto
{
    public Guid Id { get; set; }
    public Guid ExpressCompanyId { get; set; }
    public string? ExpressCompanyName { get; set; }
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
