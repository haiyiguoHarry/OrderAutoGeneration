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
[Route("api/order-conversion")]
[Authorize]
public class OrderConversionController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IOrderConversionService _conversionService;

    public OrderConversionController(AppDbContext db, IOrderConversionService conversionService)
    {
        _db = db;
        _conversionService = conversionService;
    }

    private Guid? GetUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : null;

    /// <summary>分页列表，按创建时间筛选</summary>
    [HttpGet("jobs")]
    public async Task<ActionResult<ApiResult<PagedResult<ConversionJobDto>>>> GetJobs(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? title,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        IQueryable<ConversionJob> q = _db.ConversionJobs.AsNoTracking();
        if (from.HasValue) q = q.Where(j => j.CreatedAt >= from.Value);
        if (to.HasValue) q = q.Where(j => j.CreatedAt <= to.Value);
        if (!string.IsNullOrWhiteSpace(title)) q = q.Where(j => j.Title != null && j.Title.Contains(title));
        var total = await q.CountAsync(ct);
        var ids = await q.OrderByDescending(j => j.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).Select(j => j.Id).ToListAsync(ct);
        var list = new List<ConversionJobDto>();
        if (ids.Count > 0)
        {
            var jobs = await _db.ConversionJobs.AsNoTracking()
                .Where(j => ids.Contains(j.Id))
                .Select(j => new { j.Id, j.Title, j.QuotationFileName, j.TrackingFileName, j.ExtractedDate, j.MerchantFolder, j.MerchantId, j.MerchantShopId, j.CreatedAt, j.Remark })
                .ToListAsync(ct);
            var qCounts = await _db.ConversionQuotationSheets.Where(s => ids.Contains(s.ConversionJobId)).GroupBy(s => s.ConversionJobId).Select(g => new { g.Key, C = g.Count() }).ToListAsync(ct);
            var tCounts = await _db.ConversionTrackingSheets.Where(s => ids.Contains(s.ConversionJobId)).GroupBy(s => s.ConversionJobId).Select(g => new { g.Key, C = g.Count() }).ToListAsync(ct);
            var qDict = qCounts.ToDictionary(x => x.Key, x => x.C);
            var tDict = tCounts.ToDictionary(x => x.Key, x => x.C);
            var merchantIds = jobs.Where(x => x.MerchantId.HasValue).Select(x => x.MerchantId!.Value).Distinct().ToList();
            var merchants = merchantIds.Count > 0 ? await _db.Merchants.AsNoTracking().Where(m => merchantIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Name, ct) : new Dictionary<Guid, string>();
            var shopIds = jobs.Where(x => x.MerchantShopId.HasValue).Select(x => x.MerchantShopId!.Value).Distinct().ToList();
            var shops = shopIds.Count > 0 ? await _db.MerchantShops.AsNoTracking().Where(s => shopIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct) : new Dictionary<Guid, string>();
            foreach (var j in jobs.OrderByDescending(x => x.CreatedAt))
            {
                list.Add(new ConversionJobDto
                {
                    Id = j.Id,
                    Title = j.Title ?? "",
                    QuotationFileName = j.QuotationFileName,
                    TrackingFileName = j.TrackingFileName,
                    ExtractedDate = j.ExtractedDate,
                    MerchantFolder = j.MerchantFolder,
                    MerchantId = j.MerchantId,
                    MerchantShopId = j.MerchantShopId,
                    MerchantName = j.MerchantId.HasValue ? merchants.GetValueOrDefault(j.MerchantId.Value) : null,
                    MerchantShopName = j.MerchantShopId.HasValue ? shops.GetValueOrDefault(j.MerchantShopId.Value) : null,
                    CreatedAt = j.CreatedAt,
                    Remark = j.Remark,
                    QuotationSheetCount = qDict.GetValueOrDefault(j.Id, 0),
                    TrackingSheetCount = tDict.GetValueOrDefault(j.Id, 0)
                });
            }
        }
        return Ok(ApiResult<PagedResult<ConversionJobDto>>.Ok(new PagedResult<ConversionJobDto>
            { List = list, Total = total, Page = page, PageSize = pageSize }));
    }

    /// <summary>获取单条任务详情（含 Sheet 摘要，不含大 JSON）</summary>
    [HttpGet("jobs/{id}")]
    public async Task<ActionResult<ApiResult<ConversionJobDetailDto>>> GetJob(Guid id, CancellationToken ct = default)
    {
        var j = await _db.ConversionJobs.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.QuotationFileName,
                x.TrackingFileName,
                x.ExtractedDate,
                x.MerchantFolder,
                x.MerchantId,
                x.MerchantShopId,
                x.CreatedAt,
                x.Remark,
                QuotationSheets = x.QuotationSheets.Select(s => new SheetSummaryDto { Id = s.Id, SheetName = s.SheetName, SheetType = s.SheetType }).ToList(),
                TrackingSheets = x.TrackingSheets.Select(s => new SheetSummaryDto { Id = s.Id, SheetName = s.SheetName }).ToList()
            })
            .FirstOrDefaultAsync(ct);
        if (j == null) return NotFound();
        string? merchantName = null, merchantShopName = null;
        if (j.MerchantId.HasValue)
        {
            var m = await _db.Merchants.AsNoTracking().Where(x => x.Id == j.MerchantId.Value).Select(x => x.Name).FirstOrDefaultAsync(ct);
            merchantName = m;
        }
        if (j.MerchantShopId.HasValue)
        {
            var s = await _db.MerchantShops.AsNoTracking().Where(x => x.Id == j.MerchantShopId.Value).Select(x => x.Name).FirstOrDefaultAsync(ct);
            merchantShopName = s;
        }
        var dto = new ConversionJobDetailDto
        {
            Id = j.Id,
            Title = j.Title ?? "",
            QuotationFileName = j.QuotationFileName,
            TrackingFileName = j.TrackingFileName,
            ExtractedDate = j.ExtractedDate,
            MerchantFolder = j.MerchantFolder,
            MerchantId = j.MerchantId,
            MerchantShopId = j.MerchantShopId,
            MerchantName = merchantName,
            MerchantShopName = merchantShopName,
            CreatedAt = j.CreatedAt,
            Remark = j.Remark,
            QuotationSheets = j.QuotationSheets,
            TrackingSheets = j.TrackingSheets,
            QuotationSheetCount = j.QuotationSheets.Count,
            TrackingSheetCount = j.TrackingSheets.Count
        };
        return Ok(ApiResult<ConversionJobDetailDto>.Ok(dto));
    }

    /// <summary>新建转换任务</summary>
    [HttpPost("jobs")]
    public async Task<ActionResult<ApiResult<Guid>>> CreateJob([FromBody] ConversionJobCreateDto dto, CancellationToken ct = default)
    {
        if (!dto.MerchantId.HasValue || dto.MerchantId.Value == Guid.Empty)
            return BadRequest(ApiResult<Guid>.Fail("请选择商家"));
        var merchant = await _db.Merchants.FindAsync(new object[] { dto.MerchantId.Value }, ct);
        if (merchant == null) return BadRequest(ApiResult<Guid>.Fail("商家不存在"));
        Guid? merchantShopId = dto.MerchantShopId;
        if (merchantShopId.HasValue && merchantShopId.Value != Guid.Empty)
        {
            var shop = await _db.MerchantShops.FirstOrDefaultAsync(s => s.Id == merchantShopId.Value && s.MerchantId == dto.MerchantId.Value, ct);
            if (shop == null) return BadRequest(ApiResult<Guid>.Fail("店铺不存在或不属于该商家"));
        }
        else merchantShopId = null;

        var job = new ConversionJob
        {
            Id = Guid.NewGuid(),
            Title = dto.Title ?? "未命名",
            MerchantFolder = dto.MerchantFolder,
            MerchantId = dto.MerchantId,
            MerchantShopId = merchantShopId,
            Remark = dto.Remark,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = GetUserId()
        };
        _db.ConversionJobs.Add(job);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResult<Guid>.Ok(job.Id));
    }

    /// <summary>更新任务（标题、备注、商家/店铺等）</summary>
    [HttpPut("jobs/{id}")]
    public async Task<ActionResult<ApiResult<object>>> UpdateJob(Guid id, [FromBody] ConversionJobUpdateDto dto, CancellationToken ct = default)
    {
        var job = await _db.ConversionJobs.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (job == null) return NotFound();
        if (dto.Title != null) job.Title = dto.Title;
        if (dto.MerchantFolder != null) job.MerchantFolder = dto.MerchantFolder;
        if (dto.Remark != null) job.Remark = dto.Remark;
        if (dto.MerchantId.HasValue)
        {
            if (dto.MerchantId.Value == Guid.Empty) job.MerchantId = null;
            else
            {
                var merchant = await _db.Merchants.FindAsync(new object[] { dto.MerchantId.Value }, ct);
                if (merchant == null) return BadRequest(ApiResult<object>.Fail("商家不存在"));
                job.MerchantId = dto.MerchantId;
                if (!dto.MerchantShopId.HasValue || dto.MerchantShopId.Value == Guid.Empty)
                    job.MerchantShopId = null;
                else
                {
                    var shop = await _db.MerchantShops.FirstOrDefaultAsync(s => s.Id == dto.MerchantShopId.Value && s.MerchantId == dto.MerchantId.Value, ct);
                    if (shop == null) return BadRequest(ApiResult<object>.Fail("店铺不存在或不属于该商家"));
                    job.MerchantShopId = dto.MerchantShopId;
                }
            }
        }
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResult<object>.Ok(new { }));
    }

    /// <summary>删除任务（级联删除所有 Sheet 数据）</summary>
    [HttpDelete("jobs/{id}")]
    public async Task<ActionResult<ApiResult<object>>> DeleteJob(Guid id, CancellationToken ct = default)
    {
        var job = await _db.ConversionJobs.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (job == null) return NotFound();
        _db.ConversionJobs.Remove(job);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResult<object>.Ok(new { }));
    }

    /// <summary>上传报价新 Excel，解析并存入该任务</summary>
    [HttpPost("jobs/{id}/upload-quotation")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 50 * 1024 * 1024)]
    public async Task<ActionResult<ApiResult<object>>> UploadQuotation(Guid id, IFormFile? file, CancellationToken ct = default)
    {
        if (file == null) return BadRequest(ApiResult<object>.Fail("请选择报价新 xlsx 文件"));
        try
        {
            await using var stream = file.OpenReadStream();
            await _conversionService.ParseAndSaveQuotationAsync(id, stream, file.FileName, ct);
            return Ok(ApiResult<object>.Ok(new { message = "上传并解析成功" }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResult<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResult<object>.Fail("解析失败: " + ex.Message));
        }
    }

    /// <summary>上传 Tracking&amp;Cost Excel，解析并存入该任务</summary>
    [HttpPost("jobs/{id}/upload-tracking")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 50 * 1024 * 1024)]
    public async Task<ActionResult<ApiResult<object>>> UploadTracking(Guid id, IFormFile? file, CancellationToken ct = default)
    {
        if (file == null) return BadRequest(ApiResult<object>.Fail("请选择 Tracking&Cost xlsx 文件"));
        try
        {
            await using var stream = file.OpenReadStream();
            await _conversionService.ParseAndSaveTrackingAsync(id, stream, file.FileName, ct);
            return Ok(ApiResult<object>.Ok(new { message = "上传并解析成功" }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResult<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResult<object>.Fail("解析失败: " + ex.Message));
        }
    }

    /// <summary>执行转换，返回是否成功（实际文件通过 export 接口下载）；并同步到订单管理</summary>
    [HttpPost("jobs/{id}/convert")]
    public async Task<ActionResult<ApiResult<OrderConversionResultDto>>> Convert(Guid id, CancellationToken ct = default)
    {
        try
        {
            var (onlyShip, total, purchase) = await _conversionService.ConvertAsync(id, ct);
            var result = new OrderConversionResultDto
            {
                Success = true,
                Message = "转换成功，请通过「导出」下载三份 Excel。",
                HasOnlyShip = onlyShip != null,
                HasTotal = total != null,
                HasPurchase = purchase != null
            };
            try
            {
                var sync = await _conversionService.SyncConvertedOrdersToDbAsync(id, ct);
                result.OrdersSynced = sync.OrdersCreated + sync.OrdersUpdated;
                result.OrderItemsSynced = sync.OrderItemsCreated;
                if (result.OrdersSynced > 0)
                    result.Message += " " + (sync.Message ?? $"已同步 {result.OrdersSynced} 笔订单到订单管理。");
            }
            catch (InvalidOperationException ex)
            {
                result.SyncWarning = ex.Message;
            }
            return Ok(ApiResult<OrderConversionResultDto>.Ok(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResult<OrderConversionResultDto>.Ok(new OrderConversionResultDto { Success = false, Message = ex.Message }));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResult<OrderConversionResultDto>.Ok(new OrderConversionResultDto { Success = false, Message = "转换失败: " + ex.Message }));
        }
    }

    /// <summary>导出 onlyShip_...tracking &amp; cost.xlsx</summary>
    [HttpGet("jobs/{id}/export/only-ship")]
    public async Task<IActionResult> ExportOnlyShip(Guid id, CancellationToken ct = default)
    {
        try
        {
            var (onlyShip, _, _) = await _conversionService.ConvertAsync(id, ct);
            if (onlyShip == null) return NotFound();
            var job = await _db.ConversionJobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            var fileName = $"onlyShip_{job?.Title ?? id.ToString()} tracking & cost.xlsx";
            return File(onlyShip, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (InvalidOperationException) { return NotFound(); }
    }

    /// <summary>导出 ...tracking &amp; cost.xlsx（Total to 列）</summary>
    [HttpGet("jobs/{id}/export/total")]
    public async Task<IActionResult> ExportTotal(Guid id, CancellationToken ct = default)
    {
        try
        {
            var (_, total, _) = await _conversionService.ConvertAsync(id, ct);
            if (total == null) return NotFound();
            var job = await _db.ConversionJobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            var fileName = $"{job?.Title ?? id.ToString()} tracking & cost.xlsx";
            return File(total, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (InvalidOperationException) { return NotFound(); }
    }

    /// <summary>导出 ...采购.xlsx</summary>
    [HttpGet("jobs/{id}/export/purchase")]
    public async Task<IActionResult> ExportPurchase(Guid id, CancellationToken ct = default)
    {
        try
        {
            var (_, _, purchase) = await _conversionService.ConvertAsync(id, ct);
            if (purchase == null) return NotFound();
            var job = await _db.ConversionJobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            var fileName = $"{job?.Title ?? id.ToString()} 采购.xlsx";
            return File(purchase, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (InvalidOperationException) { return NotFound(); }
    }
}

public class ConversionJobDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public string? QuotationFileName { get; set; }
    public string? TrackingFileName { get; set; }
    public DateTime? ExtractedDate { get; set; }
    public string? MerchantFolder { get; set; }
    public Guid? MerchantId { get; set; }
    public Guid? MerchantShopId { get; set; }
    public string? MerchantName { get; set; }
    public string? MerchantShopName { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Remark { get; set; }
    public int QuotationSheetCount { get; set; }
    public int TrackingSheetCount { get; set; }
}

public class ConversionJobDetailDto : ConversionJobDto
{
    public List<SheetSummaryDto> QuotationSheets { get; set; } = new();
    public List<SheetSummaryDto> TrackingSheets { get; set; } = new();
}

public class SheetSummaryDto
{
    public Guid Id { get; set; }
    public string SheetName { get; set; } = null!;
    public int SheetType { get; set; }
}

public class ConversionJobCreateDto
{
    public string? Title { get; set; }
    public string? MerchantFolder { get; set; }
    public Guid? MerchantId { get; set; }
    public Guid? MerchantShopId { get; set; }
    public string? Remark { get; set; }
}

public class ConversionJobUpdateDto
{
    public string? Title { get; set; }
    public string? MerchantFolder { get; set; }
    public Guid? MerchantId { get; set; }
    public Guid? MerchantShopId { get; set; }
    public string? Remark { get; set; }
}

public class OrderConversionResultDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public bool HasOnlyShip { get; set; }
    public bool HasTotal { get; set; }
    public bool HasPurchase { get; set; }
    /// <summary>同步到订单管理的订单数</summary>
    public int OrdersSynced { get; set; }
    /// <summary>同步的订单明细数</summary>
    public int OrderItemsSynced { get; set; }
    /// <summary>未同步时的提示（如未选商家）</summary>
    public string? SyncWarning { get; set; }
}
