using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HelloOrder.Application.Services;
using HelloOrder.Core.Entities;
using HelloOrder.Core.Enums;
using HelloOrder.Infrastructure;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HelloOrder.Infrastructure.Services;

public class OrderConversionService : IOrderConversionService
{
    private readonly AppDbContext _db;
    private readonly ILogger<OrderConversionService> _logger;

    public OrderConversionService(AppDbContext db, ILogger<OrderConversionService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task ParseAndSaveQuotationAsync(Guid jobId, Stream xlsxStream, string? fileName, CancellationToken ct = default)
    {
        var job = await _db.ConversionJobs.Include(j => j.QuotationSheets).FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job == null) throw new InvalidOperationException("转换任务不存在");

        using var workbook = new XLWorkbook(xlsxStream);
        foreach (var worksheet in workbook.Worksheets)
        {
            if (worksheet.Name.Contains("WpsReserved", StringComparison.OrdinalIgnoreCase) ||
                worksheet.Name.Contains("Reserved", StringComparison.OrdinalIgnoreCase))
                continue;
            var rangeUsed = worksheet.RangeUsed();
            if (rangeUsed == null) continue;

            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
            var lastCol = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
            if (lastRow < 1 || lastCol < 1) continue;

            var headers = new List<string>();
            for (int c = 1; c <= lastCol; c++)
            {
                var v = worksheet.Cell(1, c).Value.ToString();
                headers.Add(string.IsNullOrEmpty(v) ? $"Col{c}" : v);
            }

            var rows = new List<List<string>>();
            for (int r = 2; r <= lastRow; r++)
            {
                var row = new List<string>();
                for (int c = 1; c <= lastCol; c++)
                    row.Add(worksheet.Cell(r, c).Value.ToString());
                rows.Add(row);
            }

            int sheetType = 0; // 找货表
            if (worksheet.Name.Contains("找货", StringComparison.OrdinalIgnoreCase))
                sheetType = 0;
            else if (worksheet.Name.Contains("upsell", StringComparison.OrdinalIgnoreCase))
                sheetType = 2;
            else
                sheetType = 1; // 主报价表

            var existing = job.QuotationSheets.FirstOrDefault(s => s.SheetName == worksheet.Name);
            if (existing != null)
                _db.ConversionQuotationSheets.Remove(existing);

            var sheet = new ConversionQuotationSheet
            {
                Id = Guid.NewGuid(),
                ConversionJobId = jobId,
                SheetName = worksheet.Name,
                SheetType = sheetType,
                ColumnNamesJson = JsonSerializer.Serialize(headers),
                ContentJson = JsonSerializer.Serialize(rows),
                CreatedAt = DateTime.UtcNow
            };
            _db.ConversionQuotationSheets.Add(sheet);
        }

        if (!string.IsNullOrEmpty(fileName))
        {
            job.QuotationFileName = fileName;
            job.ExtractedDate = ExtractDateFromFileName(fileName) ?? job.ExtractedDate;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task ParseAndSaveTrackingAsync(Guid jobId, Stream xlsxStream, string? fileName, CancellationToken ct = default)
    {
        var job = await _db.ConversionJobs.Include(j => j.TrackingSheets).FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job == null) throw new InvalidOperationException("转换任务不存在");

        using var workbook = new XLWorkbook(xlsxStream);
        foreach (var worksheet in workbook.Worksheets)
        {
            if (worksheet.Name.Contains("WpsReserved", StringComparison.OrdinalIgnoreCase) ||
                worksheet.Name.Contains("Reserved", StringComparison.OrdinalIgnoreCase))
                continue;
            var rangeUsed = worksheet.RangeUsed();
            if (rangeUsed == null) continue;

            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
            var lastCol = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
            if (lastRow < 1 || lastCol < 1) continue;

            var headers = new List<string>();
            for (int c = 1; c <= lastCol; c++)
            {
                var v = worksheet.Cell(1, c).Value.ToString();
                headers.Add(string.IsNullOrEmpty(v) ? $"Col{c}" : v);
            }

            var rows = new List<List<string>>();
            for (int r = 2; r <= lastRow; r++)
            {
                var row = new List<string>();
                for (int c = 1; c <= lastCol; c++)
                    row.Add(worksheet.Cell(r, c).Value.ToString());
                rows.Add(row);
            }

            var existing = job.TrackingSheets.FirstOrDefault(s => s.SheetName == worksheet.Name);
            if (existing != null)
                _db.ConversionTrackingSheets.Remove(existing);

            var sheet = new ConversionTrackingSheet
            {
                Id = Guid.NewGuid(),
                ConversionJobId = jobId,
                SheetName = worksheet.Name,
                ColumnNamesJson = JsonSerializer.Serialize(headers),
                ContentJson = JsonSerializer.Serialize(rows),
                CreatedAt = DateTime.UtcNow
            };
            _db.ConversionTrackingSheets.Add(sheet);
        }

        if (!string.IsNullOrEmpty(fileName))
        {
            job.TrackingFileName = fileName;
            job.ExtractedDate = ExtractDateFromFileName(fileName) ?? job.ExtractedDate;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<(Stream? onlyShip, Stream? total, Stream? purchase)> ConvertAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await _db.ConversionJobs
            .Include(j => j.QuotationSheets)
            .Include(j => j.TrackingSheets)
            .FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job == null) throw new InvalidOperationException("转换任务不存在");

        var trackingSheet = job.TrackingSheets.OrderBy(s => s.SheetName).FirstOrDefault();
        if (trackingSheet == null) throw new InvalidOperationException("未上传 Tracking&Cost 文件");

        var mainQuotation = job.QuotationSheets.FirstOrDefault(s => s.SheetType == 1);
        var upsellQuotation = job.QuotationSheets.FirstOrDefault(s => s.SheetType == 2);
        var sourcingSheet = job.QuotationSheets.FirstOrDefault(s => s.SheetType == 0);

        var trackingTable = JsonToDataTable(trackingSheet.ColumnNamesJson!, trackingSheet.ContentJson);
        if (trackingTable.Rows.Count == 0) throw new InvalidOperationException("Tracking 表无数据");

        var mainQuotationTable = mainQuotation != null ? JsonToDataTable(mainQuotation.ColumnNamesJson!, mainQuotation.ContentJson) : null;
        var upsellQuotationTable = upsellQuotation != null ? JsonToDataTable(upsellQuotation.ColumnNamesJson!, upsellQuotation.ContentJson) : null;
        var sourcingTable = sourcingSheet != null ? JsonToDataTable(sourcingSheet.ColumnNamesJson!, sourcingSheet.ContentJson) : null;

        var engine = new OrderConversionEngine(_logger, mainQuotationTable, upsellQuotationTable, sourcingTable);

        var dataOnlyShip = trackingTable.Copy();
        engine.ProcessOrdersData(dataOnlyShip);

        var dataTotal = trackingTable.Copy();
        engine.ProcessOrdersDataForTotalToFr(dataTotal);

        var onlyShipStream = new MemoryStream();
        var totalStream = new MemoryStream();
        engine.SaveAsExcel(dataOnlyShip, onlyShipStream, "Sheet1");
        engine.SaveAsExcel(dataTotal, totalStream, "Sheet1");

        MemoryStream? purchaseStream = null;
        try
        {
            var purchaseTable = engine.GeneratePurchaseTable(trackingTable);
            if (purchaseTable != null && purchaseTable.Rows.Count > 0)
            {
                purchaseStream = new MemoryStream();
                engine.SavePurchaseTableAsExcel(purchaseTable, purchaseStream, job.Title ?? "采购");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "生成采购表失败");
        }

        onlyShipStream.Position = 0;
        totalStream.Position = 0;
        if (purchaseStream != null) purchaseStream.Position = 0;
        await Task.CompletedTask;
        return (onlyShipStream, totalStream, purchaseStream);
    }

    public async Task<SyncToOrdersResult> SyncConvertedOrdersToDbAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await _db.ConversionJobs
            .Include(j => j.QuotationSheets)
            .Include(j => j.TrackingSheets)
            .FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job == null) throw new InvalidOperationException("转换任务不存在");
        if (!job.MerchantId.HasValue) throw new InvalidOperationException("请先在任务中选择商家后再同步到订单");

        var trackingSheet = job.TrackingSheets.OrderBy(s => s.SheetName).FirstOrDefault();
        if (trackingSheet == null) throw new InvalidOperationException("未上传 Tracking&Cost 文件");

        var mainQuotation = job.QuotationSheets.FirstOrDefault(s => s.SheetType == 1);
        var upsellQuotation = job.QuotationSheets.FirstOrDefault(s => s.SheetType == 2);
        var sourcingSheet = job.QuotationSheets.FirstOrDefault(s => s.SheetType == 0);
        var trackingTable = JsonToDataTable(trackingSheet.ColumnNamesJson!, trackingSheet.ContentJson);
        if (trackingTable.Rows.Count == 0) throw new InvalidOperationException("Tracking 表无数据");

        var mainQuotationTable = mainQuotation != null ? JsonToDataTable(mainQuotation.ColumnNamesJson!, mainQuotation.ContentJson) : null;
        var upsellQuotationTable = upsellQuotation != null ? JsonToDataTable(upsellQuotation.ColumnNamesJson!, upsellQuotation.ContentJson) : null;
        var sourcingTable = sourcingSheet != null ? JsonToDataTable(sourcingSheet.ColumnNamesJson!, sourcingSheet.ContentJson) : null;
        var engine = new OrderConversionEngine(_logger, mainQuotationTable, upsellQuotationTable, sourcingTable);
        engine.ProcessOrdersData(trackingTable);

        string? orderColumn = null, skuColumn = null, qtyColumn = null, productNameColumn = null, costColumn = "cost";
        foreach (System.Data.DataColumn col in trackingTable.Columns)
        {
            var cn = col.ColumnName.Trim();
            if ((cn.Contains("Order#") || (cn.Contains("Order") && cn.Contains("#"))) && orderColumn == null) orderColumn = col.ColumnName;
            if (cn.Contains("商品SKU", StringComparison.OrdinalIgnoreCase)) skuColumn = col.ColumnName;
            if (cn.Contains("QTY", StringComparison.OrdinalIgnoreCase) && cn.Contains("Merged", StringComparison.OrdinalIgnoreCase)) qtyColumn = col.ColumnName;
            if ((cn.Contains("Product name", StringComparison.OrdinalIgnoreCase) || cn.Contains("产品名称", StringComparison.OrdinalIgnoreCase) || (cn.Contains("Item", StringComparison.OrdinalIgnoreCase) && cn.Contains("name", StringComparison.OrdinalIgnoreCase))) && productNameColumn == null) productNameColumn = col.ColumnName;
        }
        if (orderColumn == null || skuColumn == null || qtyColumn == null)
            throw new InvalidOperationException("Tracking 表缺少订单号或商品SKU或数量列，无法同步");

        var orderGroups = trackingTable.Rows.Cast<System.Data.DataRow>()
            .Where(r => !IsSumRow(r, orderColumn))
            .GroupBy(r => r[orderColumn]?.ToString()?.Trim() ?? "")
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .ToList();

        int ordersCreated = 0, ordersUpdated = 0, orderItemsCreated = 0;
        var now = DateTime.UtcNow;
        var merchantId = job.MerchantId!.Value;

        foreach (var group in orderGroups)
        {
            var orderNo = group.Key;
            var rows = group.Where(r => ParseQty(r[qtyColumn]?.ToString()) > 0).ToList();
            if (rows.Count == 0) continue;

            var existingOrder = await _db.Orders.FirstOrDefaultAsync(o => o.ConversionJobId == jobId && o.OrderNo == orderNo, ct);
            decimal orderTotal = 0;
            foreach (var r in rows)
            {
                var costStr = trackingTable.Columns.Contains(costColumn) ? r[costColumn]?.ToString() : null;
                if (decimal.TryParse(costStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var costVal))
                    orderTotal += costVal;
            }

            if (existingOrder != null)
            {
                existingOrder.TotalAmount = orderTotal;
                existingOrder.OrderTime = job.ExtractedDate;
                existingOrder.UpdatedAt = now;
                _db.OrderItems.RemoveRange(await _db.OrderItems.Where(i => i.OrderId == existingOrder.Id).ToListAsync(ct));
                ordersUpdated++;
            }
            else
            {
                existingOrder = new Order
                {
                    Id = Guid.NewGuid(),
                    MerchantId = merchantId,
                    MerchantShopId = job.MerchantShopId,
                    OrderNo = orderNo,
                    Status = OrderStatus.PendingQuote,
                    TotalAmount = orderTotal,
                    Currency = "CNY",
                    OrderTime = job.ExtractedDate,
                    ConversionJobId = jobId,
                    BusinessUserId = job.CreatedByUserId,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _db.Orders.Add(existingOrder);
                ordersCreated++;
            }

            foreach (var r in rows)
            {
                var qty = ParseQty(r[qtyColumn]?.ToString());
                if (qty <= 0) continue;
                var sku = r[skuColumn]?.ToString()?.Trim() ?? "";
                var productName = productNameColumn != null ? r[productNameColumn]?.ToString()?.Trim() ?? "" : "";
                var costStr = trackingTable.Columns.Contains(costColumn) ? r[costColumn]?.ToString() : null;
                decimal.TryParse(costStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var price);
                _db.OrderItems.Add(new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = existingOrder!.Id,
                    Sku = sku,
                    ProductName = productName,
                    Quantity = qty,
                    Price = price,
                    PurchasePrice = price
                });
                orderItemsCreated++;
            }
        }

        await _db.SaveChangesAsync(ct);
        return new SyncToOrdersResult
        {
            OrdersCreated = ordersCreated,
            OrdersUpdated = ordersUpdated,
            OrderItemsCreated = orderItemsCreated,
            Message = $"已同步 {ordersCreated + ordersUpdated} 笔订单、{orderItemsCreated} 条明细到订单管理。"
        };
    }

    private static bool IsSumRow(System.Data.DataRow row, string orderColumn)
    {
        var v = row[orderColumn]?.ToString()?.Trim() ?? "";
        return string.Equals(v, "sum", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(v);
    }

    private static int ParseQty(string? value) => int.TryParse(value, out var q) ? q : 0;

    private static System.Data.DataTable JsonToDataTable(string columnNamesJson, string contentJson)
    {
        var headers = JsonSerializer.Deserialize<List<string>>(columnNamesJson) ?? new List<string>();
        var rows = JsonSerializer.Deserialize<List<List<string>>>(contentJson) ?? new List<List<string>>();
        var dt = new System.Data.DataTable();
        foreach (var h in headers)
            dt.Columns.Add(string.IsNullOrEmpty(h) ? "Col" + dt.Columns.Count : h, typeof(string));
        foreach (var row in rows)
        {
            var dr = dt.NewRow();
            for (int i = 0; i < Math.Min(row.Count, dt.Columns.Count); i++)
                dr[i] = row[i] ?? "";
            dt.Rows.Add(dr);
        }
        return dt;
    }

    private static DateTime? ExtractDateFromFileName(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var m = Regex.Match(name, @"(\d{4})(\d{2})(\d{2})");
        if (m.Success && int.TryParse(m.Groups[1].Value, out var y) && int.TryParse(m.Groups[2].Value, out var mo) && int.TryParse(m.Groups[3].Value, out var d))
            if (mo >= 1 && mo <= 12 && d >= 1 && d <= 31)
                return new DateTime(y, mo, d);
        return null;
    }
}
