using System.Security.Claims;
using HelloOrder.Api.Common;
using HelloOrder.Application.Services;
using HelloOrder.Core.Enums;
using HelloOrder.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelloOrder.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IDataScopeService _dataScope;

    public ReportsController(AppDbContext db, IDataScopeService dataScope)
    {
        _db = db;
        _dataScope = dataScope;
    }

    [HttpGet("sales")]
    public ActionResult<ApiResult<object>> Sales([FromQuery] string? from, [FromQuery] string? to)
    {
        return Ok(ApiResult<object>.Ok(new { totalOrders = 0, totalAmount = 0m, items = Array.Empty<object>() }));
    }

    [HttpGet("profit")]
    public ActionResult<ApiResult<object>> Profit([FromQuery] string? from, [FromQuery] string? to)
    {
        return Ok(ApiResult<object>.Ok(new { revenue = 0m, cost = 0m, profit = 0m }));
    }

    [HttpGet("commission")]
    public ActionResult<ApiResult<object>> Commission([FromQuery] string? period)
    {
        return Ok(ApiResult<object>.Ok(new { list = Array.Empty<object>() }));
    }

    /// <summary>商家每日付款统计：按商家、日期范围汇总已付款订单的 paid_at，按日汇总金额与笔数</summary>
    [HttpGet("merchant-daily-payment")]
    public async Task<ActionResult<ApiResult<MerchantDailyPaymentResult>>> MerchantDailyPayment(
        [FromQuery] Guid? merchantId,
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        var role = GetRoleCode();
        var visibleIds = await _dataScope.GetVisibleBusinessUserIdsAsync(userId, role, ct);
        var q = _db.Orders.AsNoTracking()
            .Where(o => o.Status >= OrderStatus.Paid && o.PaidAt != null);
        if (visibleIds != null)
        {
            if (visibleIds.Count == 0) return Ok(ApiResult<MerchantDailyPaymentResult>.Ok(new MerchantDailyPaymentResult { Daily = new List<MerchantDailyPaymentItem>(), TotalAmount = 0, TotalCount = 0 }));
            q = q.Where(o => o.BusinessUserId != null && visibleIds.Contains(o.BusinessUserId.Value));
        }
        if (merchantId.HasValue)
            q = q.Where(o => o.MerchantId == merchantId.Value);
        DateTime? fromDate = null;
        DateTime? toDate = null;
        if (DateTime.TryParse(from, out var fd)) fromDate = fd.Date;
        if (DateTime.TryParse(to, out var td)) toDate = td.Date.AddDays(1);
        if (fromDate.HasValue) q = q.Where(o => o.PaidAt >= fromDate.Value);
        if (toDate.HasValue) q = q.Where(o => o.PaidAt < toDate.Value);

        var list = await q.ToListAsync(ct);
        var byDay = list
            .GroupBy(o => o.PaidAt!.Value.Date)
            .Select(g => new MerchantDailyPaymentItem
            {
                Date = g.Key,
                Amount = g.Sum(o => o.TotalAmount),
                OrderCount = g.Count()
            })
            .OrderBy(x => x.Date)
            .ToList();
        return Ok(ApiResult<MerchantDailyPaymentResult>.Ok(new MerchantDailyPaymentResult
        {
            Daily = byDay,
            TotalAmount = byDay.Sum(x => x.Amount),
            TotalCount = byDay.Sum(x => x.OrderCount)
        }));
    }

    private Guid? GetUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var uid) ? uid : null;
    }

    private string? GetRoleCode() => User.FindFirstValue("role_code") ?? "";
}

public class MerchantDailyPaymentItem
{
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public int OrderCount { get; set; }
}

public class MerchantDailyPaymentResult
{
    public List<MerchantDailyPaymentItem> Daily { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public int TotalCount { get; set; }
}
