using System.Data;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;

namespace HelloOrder.Infrastructure.Services;

/// <summary>订单转换核心逻辑（移植自 OrderConverterEXE），使用传入的报价/找货 DataTable</summary>
internal class OrderConversionEngine
{
    private readonly ILogger _logger;
    private readonly DataTable? _mainQuotation;
    private readonly DataTable? _upsellQuotation;
    private readonly DataTable? _sourcingTable;

    public OrderConversionEngine(ILogger logger, DataTable? mainQuotation, DataTable? upsellQuotation, DataTable? sourcingTable)
    {
        _logger = logger;
        _mainQuotation = mainQuotation;
        _upsellQuotation = upsellQuotation;
        _sourcingTable = sourcingTable;
    }

    public void ProcessOrdersData(DataTable data)
    {
        if (data.Rows.Count == 0) return;
        FixMissingProductSku(data);
        RemoveUnnecessaryColumns(data);
        AddSkuQuotationColumn(data);
        AddQtyMergedColumn(data);
        AddCostAndUpsellColumns(data);
        AddSumRow(data);
    }

    public void ProcessOrdersDataForTotalToFr(DataTable data)
    {
        if (data.Rows.Count == 0) return;
        FixMissingProductSku(data);
        RemoveUnnecessaryColumns(data);
        AddSkuQuotationColumn(data);
        AddQtyMergedColumn(data);
        AddCostAndUpsellColumnsForTotalToFr(data);
        AddSumRow(data);
    }

    public DataTable? GeneratePurchaseTable(DataTable ordersData)
    {
        var clone = ordersData.Clone();
        foreach (DataRow r in ordersData.Rows)
            clone.ImportRow(r);
        FixMissingProductSku(clone);

        string? productSkuColumn = null, qtyColumn = null, productNameColumn = null;
        foreach (DataColumn col in clone.Columns)
        {
            var cn = col.ColumnName.Trim();
            if (cn.Contains("商品SKU", StringComparison.OrdinalIgnoreCase)) productSkuColumn = col.ColumnName;
            if (cn.Contains("QTY", StringComparison.OrdinalIgnoreCase) && !cn.Contains("MERGED", StringComparison.OrdinalIgnoreCase)) qtyColumn = col.ColumnName;
            if (cn.Contains("Product name", StringComparison.OrdinalIgnoreCase) || cn.Contains("产品名称", StringComparison.OrdinalIgnoreCase)) productNameColumn = col.ColumnName;
        }
        if (productSkuColumn == null || qtyColumn == null) return null;

        var skuGroups = clone.Rows.Cast<DataRow>()
            .GroupBy(row => row[productSkuColumn]?.ToString()?.Trim() ?? "")
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .Select(g => new
            {
                Sku = g.Key,
                TotalQty = g.Sum(row => int.TryParse(row[qtyColumn]?.ToString(), out var q) ? q : 0),
                ProductName = productNameColumn != null ? g.Select(row => row[productNameColumn]?.ToString()?.Trim() ?? "").FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "" : ""
            })
            .Where(x => x.TotalQty > 0)
            .OrderBy(x => x.Sku)
            .ToList();

        if (skuGroups.Count == 0) return null;

        var purchaseData = new DataTable();
        purchaseData.Columns.Add("SKU", typeof(string));
        purchaseData.Columns.Add("SKU_商品名", typeof(string));
        purchaseData.Columns.Add("size", typeof(string));
        purchaseData.Columns.Add("图片", typeof(string));
        purchaseData.Columns.Add("应采", typeof(int));
        purchaseData.Columns.Add("快递单号", typeof(string));
        purchaseData.Columns.Add("备注", typeof(string));
        purchaseData.Columns.Add("采购链接", typeof(string));

        string? qSku = null, qName = null, qSize = null, qLink = null;
        if (_sourcingTable != null)
        {
            foreach (DataColumn c in _sourcingTable.Columns)
            {
                var cn = c.ColumnName.Trim().ToUpper();
                if (cn.Contains("SKU") && qSku == null) qSku = c.ColumnName;
                if ((cn.Contains("产品中文") || cn.Contains("商品")) && cn.Contains("名")) qName = c.ColumnName;
                if (cn.Contains("SIZE") && qSize == null) qSize = c.ColumnName;
                if ((cn.Contains("链接") || cn.Contains("LINK")) && qLink == null) qLink = c.ColumnName;
            }
        }

        foreach (var g in skuGroups)
        {
            var row = purchaseData.NewRow();
            row["SKU"] = g.Sku;
            row["应采"] = g.TotalQty;
            var parts = g.Sku.Split('-');
            if (parts.Length > 0 && parts[^1].Length <= 5)
                row["size"] = parts[^1];

            string? productChineseName = null;
            if (_sourcingTable != null && qSku != null && !string.IsNullOrWhiteSpace(ExtractSkuPrefix(g.Sku)))
            {
                var skuPrefix = ExtractSkuPrefix(g.Sku);
                var match = _sourcingTable.Rows.Cast<DataRow>().FirstOrDefault(r =>
                    string.Equals(ExtractSkuPrefix(r[qSku]?.ToString() ?? ""), skuPrefix, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    if (qName != null) productChineseName = match[qName]?.ToString()?.Trim();
                    if (qSize != null && !string.IsNullOrWhiteSpace(match[qSize]?.ToString())) row["size"] = match[qSize];
                    if (qLink != null) row["采购链接"] = match[qLink];
                }
            }

            var nameWithoutSize = RemoveSizeFromProductName(g.ProductName);
            row["SKU_商品名"] = BuildProductNameForPurchase(productChineseName, nameWithoutSize);
            purchaseData.Rows.Add(row);
        }

        var sumRow = purchaseData.NewRow();
        sumRow["图片"] = "SUM";
        sumRow["应采"] = skuGroups.Sum(x => x.TotalQty);
        purchaseData.Rows.Add(sumRow);
        return purchaseData;
    }

    public void SaveAsExcel(DataTable data, Stream outputStream, string sheetName)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add(string.IsNullOrEmpty(sheetName) ? "Sheet1" : sheetName);
        for (int c = 0; c < data.Columns.Count; c++)
            ws.Cell(1, c + 1).Value = data.Columns[c].ColumnName;
        for (int r = 0; r < data.Rows.Count; r++)
        {
            for (int c = 0; c < data.Columns.Count; c++)
            {
                var val = data.Rows[r][c];
                var cell = ws.Cell(r + 2, c + 1);
                var colName = data.Columns[c].ColumnName;
                var isCostUpsell = colName == "cost" || colName == "upsell";
                if (val?.ToString()?.StartsWith("=") == true)
                    cell.FormulaA1 = val.ToString();
                else if (val is double or int or decimal or float)
                    cell.Value = Convert.ToDouble(val);
                else
                {
                    var s = val?.ToString() ?? "";
                    if (isCostUpsell && !string.IsNullOrWhiteSpace(s) && (s.Contains("未找到") || s.Contains("不存在")))
                        cell.Value = s;
                    else if (isCostUpsell && double.TryParse(s, out var n))
                        cell.Value = n;
                    else
                        cell.Value = s;
                }
                if (isCostUpsell) cell.Style.NumberFormat.Format = "$#,##0.00";
                if (val?.ToString()?.Contains("未找到") == true || val?.ToString()?.Contains("不存在") == true)
                {
                    cell.Style.Fill.PatternType = XLFillPatternValues.Solid;
                    cell.Style.Fill.BackgroundColor = XLColor.Red;
                    cell.Style.Font.FontColor = XLColor.White;
                }
            }
        }
        ws.SheetView.FreezeRows(2);
        ws.SheetView.FreezeColumns(1);
        if (data.Rows.Count > 0)
        {
            var lastRow = data.Rows.Count + 1;
            for (int col = 0; col < data.Columns.Count; col++)
            {
                if (ws.Cell(lastRow, col + 1).Value.ToString() == "sum")
                {
                    ws.Cell(lastRow, col + 1).Style.Fill.BackgroundColor = XLColor.Yellow;
                    ws.Cell(lastRow, col + 1).Style.Font.Bold = true;
                }
            }
            var costIdx = data.Columns.IndexOf("cost") + 1;
            if (costIdx > 0)
            {
                ws.Cell(lastRow, costIdx).Style.Fill.BackgroundColor = XLColor.Yellow;
                ws.Cell(lastRow, costIdx).Style.Font.Bold = true;
            }
        }
        ws.Columns().AdjustToContents();
        workbook.SaveAs(outputStream);
    }

    public void SavePurchaseTableAsExcel(DataTable data, Stream outputStream, string baseName)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("采购");
        ws.Cell(1, 1).Value = $"LG-Le-7天库存采购-{baseName}";
        ws.Range(1, 1, 1, 8).Merge();
        ws.Cell(2, 1).Value = "业务:伍菲-助理:嘉卉-采购:陈诺 仓库:东梅";
        ws.Range(2, 1, 2, 8).Merge();
        var headers = new[] { "SKU", "SKU", "size", "图片", "应采", "快递单号", "备注", "采购链接" };
        for (int c = 0; c < headers.Length; c++) ws.Cell(3, c + 1).Value = headers[c];
        for (int r = 0; r < data.Rows.Count; r++)
        {
            var excelRow = r + 4;
            var dr = data.Rows[r];
            ws.Cell(excelRow, 1).Value = dr["SKU"]?.ToString() ?? "";
            ws.Cell(excelRow, 2).Value = dr["SKU_商品名"]?.ToString() ?? "";
            ws.Cell(excelRow, 3).Value = dr["size"]?.ToString() ?? "";
            ws.Cell(excelRow, 4).Value = dr["图片"]?.ToString() ?? "";
            if (dr["应采"] != DBNull.Value && int.TryParse(dr["应采"].ToString(), out var qty))
                ws.Cell(excelRow, 5).Value = qty;
            ws.Cell(excelRow, 6).Value = dr["快递单号"]?.ToString() ?? "";
            ws.Cell(excelRow, 7).Value = dr["备注"]?.ToString() ?? "";
            ws.Cell(excelRow, 8).Value = dr["采购链接"]?.ToString() ?? "";
            if (dr["图片"]?.ToString() == "SUM")
            {
                ws.Cell(excelRow, 5).FormulaA1 = $"=SUM(E4:E{excelRow - 1})";
                ws.Range(excelRow, 1, excelRow, 8).Style.Fill.BackgroundColor = XLColor.Yellow;
                ws.Range(excelRow, 1, excelRow, 8).Style.Font.Bold = true;
            }
        }
        ws.SheetView.FreezeRows(3);
        ws.Columns().AdjustToContents();
        workbook.SaveAs(outputStream);
    }

    private void FixMissingProductSku(DataTable data)
    {
        string? productSkuColumn = null, itemNameColumn = null;
        foreach (DataColumn col in data.Columns)
        {
            var cn = col.ColumnName.Trim();
            if (cn.Contains("商品SKU", StringComparison.OrdinalIgnoreCase)) productSkuColumn = col.ColumnName;
            if (cn.Contains("Item", StringComparison.OrdinalIgnoreCase) && cn.Contains("name", StringComparison.OrdinalIgnoreCase)) itemNameColumn = col.ColumnName;
        }
        if (productSkuColumn == null || itemNameColumn == null) return;

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DataRow row in data.Rows)
        {
            var itemName = row[itemNameColumn]?.ToString()?.Trim() ?? "";
            var sku = row[productSkuColumn]?.ToString()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(itemName) || string.IsNullOrWhiteSpace(sku)) continue;
            var key = ExtractItemNameMainPart(itemName);
            if (!map.ContainsKey(key)) map[key] = sku;
        }
        foreach (DataRow row in data.Rows)
        {
            var sku = row[productSkuColumn]?.ToString()?.Trim() ?? "";
            var itemName = row[itemNameColumn]?.ToString()?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(sku) || string.IsNullOrWhiteSpace(itemName)) continue;
            var key = ExtractItemNameMainPart(itemName);
            if (map.TryGetValue(key, out var mapped)) row[productSkuColumn] = mapped;
        }
    }

    private void RemoveUnnecessaryColumns(DataTable data)
    {
        foreach (var name in new[] { "Item cost", "Shipping cost" })
        {
            DataColumn? toRemove = null;
            foreach (DataColumn c in data.Columns)
                if (c.ColumnName.Equals(name, StringComparison.OrdinalIgnoreCase)) { toRemove = c; break; }
            if (toRemove != null) data.Columns.Remove(toRemove);
        }
    }

    private void AddSkuQuotationColumn(DataTable data)
    {
        string? productSkuColumn = null;
        foreach (DataColumn col in data.Columns)
            if (col.ColumnName.Trim().Contains("商品SKU", StringComparison.OrdinalIgnoreCase)) { productSkuColumn = col.ColumnName; break; }
        if (productSkuColumn == null) return;
        if (!data.Columns.Contains("SKU_Quotation")) data.Columns.Add("SKU_Quotation", typeof(string));
        foreach (DataRow row in data.Rows)
            row["SKU_Quotation"] = ExtractSkuPrefix(row[productSkuColumn]?.ToString()?.Trim() ?? "");
        ReorderColumn(data, "SKU_Quotation", productSkuColumn, after: true);
    }

    private void AddQtyMergedColumn(DataTable data)
    {
        string? orderColumn = null, qtyColumn = null;
        foreach (DataColumn col in data.Columns)
        {
            var cn = col.ColumnName.Trim();
            if ((cn.Contains("Order#") || (cn.Contains("Order") && cn.Contains("#"))) && orderColumn == null) orderColumn = col.ColumnName;
            if (cn.Contains("QTY", StringComparison.OrdinalIgnoreCase) && !cn.Contains("MERGED", StringComparison.OrdinalIgnoreCase) && qtyColumn == null) qtyColumn = col.ColumnName;
        }
        if (orderColumn == null || qtyColumn == null || !data.Columns.Contains("SKU_Quotation")) return;
        if (!data.Columns.Contains("QTY_Merged")) data.Columns.Add("QTY_Merged", typeof(string));
        var groups = data.Rows.Cast<DataRow>().GroupBy(r => new { Order = r[orderColumn]?.ToString() ?? "", SkuQ = r["SKU_Quotation"]?.ToString() ?? "" });
        foreach (var g in groups)
        {
            var rows = g.ToList();
            var total = rows.Sum(r => int.TryParse(r[qtyColumn]?.ToString(), out var q) ? q : 0);
            if (rows.Count > 0) rows[0]["QTY_Merged"] = total > 0 ? total.ToString() : "";
        }
        ReorderColumn(data, "QTY_Merged", qtyColumn!, after: true);
    }

    private void AddCostAndUpsellColumns(DataTable data)
    {
        string? countryColumn = null, orderColumn = null;
        foreach (DataColumn col in data.Columns)
        {
            var cn = col.ColumnName.Trim();
            if ((cn.Contains("Country") || cn.Contains("国家")) && countryColumn == null) countryColumn = col.ColumnName;
            if ((cn.Contains("Order#") || (cn.Contains("Order") && cn.Contains("#"))) && orderColumn == null) orderColumn = col.ColumnName;
        }
        if (countryColumn == null || orderColumn == null) return;
        if (!data.Columns.Contains("cost")) data.Columns.Add("cost", typeof(string));
        if (!data.Columns.Contains("upsell")) data.Columns.Add("upsell", typeof(string));
        var orderGroups = data.Rows.Cast<DataRow>().GroupBy(r => r[orderColumn]?.ToString() ?? "");
        foreach (var orderGroup in orderGroups)
        {
            var rows = orderGroup.Select((r, i) => new { Row = r, Idx = i, Qty = ParseQty(r["QTY_Merged"]?.ToString()) }).Where(x => x.Qty > 0).ToList();
            if (rows.Count == 0) continue;
            var maxRow = rows.OrderByDescending(x => x.Qty).First();
            SetCostValue(maxRow.Row, _mainQuotation, countryColumn);
            foreach (var other in rows.Where(x => x.Idx != maxRow.Idx))
                SetUpsellValue(other.Row, _upsellQuotation, countryColumn);
        }
        ReorderColumn(data, "cost", countryColumn, after: false);
        ReorderColumn(data, "upsell", "cost", after: true);
    }

    private void AddCostAndUpsellColumnsForTotalToFr(DataTable data)
    {
        string? countryColumn = null, orderColumn = null;
        foreach (DataColumn col in data.Columns)
        {
            var cn = col.ColumnName.Trim();
            if ((cn.Contains("Country") || cn.Contains("国家")) && countryColumn == null) countryColumn = col.ColumnName;
            if ((cn.Contains("Order#") || (cn.Contains("Order") && cn.Contains("#"))) && orderColumn == null) orderColumn = col.ColumnName;
        }
        if (countryColumn == null || orderColumn == null) return;
        if (!data.Columns.Contains("cost")) data.Columns.Add("cost", typeof(string));
        if (!data.Columns.Contains("upsell")) data.Columns.Add("upsell", typeof(string));
        var orderGroups = data.Rows.Cast<DataRow>().GroupBy(r => r[orderColumn]?.ToString() ?? "");
        foreach (var orderGroup in orderGroups)
        {
            var rows = orderGroup.Select((r, i) => new { Row = r, Idx = i, Qty = ParseQty(r["QTY_Merged"]?.ToString()) }).Where(x => x.Qty > 0).ToList();
            if (rows.Count == 0) continue;
            var maxRow = rows.OrderByDescending(x => x.Qty).First();
            SetCostValueByTotalToFr(maxRow.Row, _mainQuotation, countryColumn);
            foreach (var other in rows.Where(x => x.Idx != maxRow.Idx))
                SetUpsellValueByTotalToFr(other.Row, _upsellQuotation, countryColumn);
        }
        ReorderColumn(data, "cost", countryColumn, after: false);
        ReorderColumn(data, "upsell", "cost", after: true);
    }

    private void SetCostValue(DataRow row, DataTable? quotation, string countryColumn)
    {
        row["cost"] = quotation == null ? "报价表文件不存在" : (FindQuotationValue(quotation, row["SKU_Quotation"]?.ToString()?.Trim() ?? "", ParseQty(row["QTY_Merged"]?.ToString()), row[countryColumn]?.ToString()?.Trim()?.ToUpper() ?? "") ?? "未找到匹配的数据");
    }

    private void SetUpsellValue(DataRow row, DataTable? quotation, string countryColumn)
    {
        row["upsell"] = quotation == null ? "upsell报价表文件不存在" : (FindQuotationValue(quotation, row["SKU_Quotation"]?.ToString()?.Trim() ?? "", ParseQty(row["QTY_Merged"]?.ToString()), row[countryColumn]?.ToString()?.Trim()?.ToUpper() ?? "") ?? "未找到匹配的数据");
    }

    private void SetCostValueByTotalToFr(DataRow row, DataTable? quotation, string countryColumn)
    {
        row["cost"] = quotation == null ? "报价表文件不存在" : (FindQuotationValueByTotalToFr(quotation, row["SKU_Quotation"]?.ToString()?.Trim() ?? "", ParseQty(row["QTY_Merged"]?.ToString()), row[countryColumn]?.ToString()?.Trim() ?? "") ?? "未找到匹配的数据");
    }

    private void SetUpsellValueByTotalToFr(DataRow row, DataTable? quotation, string countryColumn)
    {
        row["upsell"] = quotation == null ? "upsell报价表文件不存在" : (FindQuotationValueByTotalToFr(quotation, row["SKU_Quotation"]?.ToString()?.Trim() ?? "", ParseQty(row["QTY_Merged"]?.ToString()), row[countryColumn]?.ToString()?.Trim() ?? "") ?? "未找到匹配的数据");
    }

    private string? FindQuotationValue(DataTable quotation, string skuQuotation, int qty, string countryCode)
    {
        DataColumn? skuCol = null;
        foreach (DataColumn c in quotation.Columns) if (c.ColumnName.ToUpper().Contains("SKU")) { skuCol = c; break; }
        if (skuCol == null) return null;
        var matching = quotation.Rows.Cast<DataRow>().Where(r => (r[skuCol]?.ToString()?.Trim() ?? "") == skuQuotation).ToList();
        if (matching.Count == 0) return null;
        var qtyCols = quotation.Columns.Cast<DataColumn>().Where(c => c.ColumnName.ToUpper().Contains("QTY") && !c.ColumnName.ToUpper().Contains("MERGED")).ToList();
        DataColumn? shipCol = null;
        foreach (DataColumn c in quotation.Columns)
            if (c.ColumnName.Trim().ToUpper().StartsWith("SHIP TO", StringComparison.OrdinalIgnoreCase) && c.ColumnName.ToUpper().Contains(countryCode))
            { shipCol = c; break; }
        if (shipCol == null) return null;
        foreach (var row in matching)
            foreach (var qc in qtyCols)
                if (int.TryParse(row[qc]?.ToString(), out var rq) && rq == qty)
                {
                    var v = row[shipCol]?.ToString();
                    if (!string.IsNullOrWhiteSpace(v)) return v;
                }
        return null;
    }

    private string? FindQuotationValueByTotalToFr(DataTable quotation, string skuQuotation, int qty, string countryCode)
    {
        DataColumn? skuCol = null;
        foreach (DataColumn c in quotation.Columns) if (c.ColumnName.ToUpper().Contains("SKU")) { skuCol = c; break; }
        if (skuCol == null) return null;
        var matching = quotation.Rows.Cast<DataRow>().Where(r => (r[skuCol]?.ToString()?.Trim() ?? "") == skuQuotation).ToList();
        if (matching.Count == 0) return null;
        var qtyCols = quotation.Columns.Cast<DataColumn>().Where(c => c.ColumnName.ToUpper().Contains("QTY") && !c.ColumnName.ToUpper().Contains("MERGED")).ToList();
        var totalToName = $"Total to {countryCode}";
        DataColumn? totalCol = null;
        foreach (DataColumn c in quotation.Columns)
            if (c.ColumnName.Trim().Equals(totalToName, StringComparison.OrdinalIgnoreCase)) { totalCol = c; break; }
        if (totalCol == null) return null;
        foreach (var row in matching)
            foreach (var qc in qtyCols)
                if (int.TryParse(row[qc]?.ToString(), out var rq) && rq == qty)
                {
                    var v = row[totalCol]?.ToString();
                    if (!string.IsNullOrWhiteSpace(v)) return v;
                }
        return null;
    }

    private void AddSumRow(DataTable data)
    {
        if (data.Rows.Count == 0) return;
        var newRow = data.NewRow();
        data.Rows.Add(newRow);
        var costIdx = data.Columns.IndexOf("cost");
        var upsellIdx = data.Columns.IndexOf("upsell");
        if (costIdx < 0 || upsellIdx < 0) return;
        if (costIdx > 0) newRow[costIdx - 1] = "sum";
        else newRow[0] = "sum";
        var costLetter = GetExcelColumnLetter(costIdx + 1);
        var upsellLetter = GetExcelColumnLetter(upsellIdx + 1);
        var endRow = data.Rows.Count;
        newRow["cost"] = $"=SUM({costLetter}2:{costLetter}{endRow})+SUM({upsellLetter}2:{upsellLetter}{endRow})";
    }

    private static int ParseQty(string? value) => int.TryParse(value, out var q) ? q : 0;
    private static string ExtractSkuPrefix(string sku)
    {
        if (string.IsNullOrWhiteSpace(sku)) return "";
        var parts = sku.Split('-');
        return parts.Length >= 2 ? $"{parts[0]}-{parts[1]}" : (parts.Length == 1 ? parts[0] : "");
    }
    private static string ExtractItemNameMainPart(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName)) return "";
        var t = itemName.Trim();
        var lastDash = t.LastIndexOf(" - ", StringComparison.OrdinalIgnoreCase);
        if (lastDash > 0 && t.Substring(0, lastDash).Trim().Length > 5) return t.Substring(0, lastDash).Trim();
        return t;
    }
    private static string RemoveSizeFromProductName(string productName)
    {
        if (string.IsNullOrWhiteSpace(productName)) return "";
        return Regex.Replace(productName.Trim(), @"\s*-\s*[0-9]*XL\s*$|\s*-\s*[SM]\s*$|\s*-\s*L\s*$|\s*-\s*均码\s*$", "", RegexOptions.IgnoreCase).Trim();
    }
    private static string BuildProductNameForPurchase(string? productChineseName, string productNameWithoutSize)
    {
        if (string.IsNullOrWhiteSpace(productChineseName)) return productNameWithoutSize;
        if (string.IsNullOrWhiteSpace(productNameWithoutSize)) return productChineseName;
        if (productChineseName.Equals(productNameWithoutSize, StringComparison.OrdinalIgnoreCase) ||
            productChineseName.Contains(productNameWithoutSize, StringComparison.OrdinalIgnoreCase) ||
            productNameWithoutSize.Contains(productChineseName, StringComparison.OrdinalIgnoreCase))
            return productChineseName;
        return $"{productChineseName}-{productNameWithoutSize}";
    }
    private static void ReorderColumn(DataTable data, string columnToMove, string referenceColumn, bool after)
    {
        if (!data.Columns.Contains(columnToMove) || !data.Columns.Contains(referenceColumn)) return;
        var moveCol = data.Columns[columnToMove]!;
        var refIdx = data.Columns[referenceColumn]!.Ordinal;
        var targetIdx = after ? refIdx + 1 : refIdx;
        if (moveCol.Ordinal < refIdx && after) targetIdx--;
        if (moveCol.Ordinal > refIdx && !after) targetIdx++;
        if (moveCol.Ordinal != targetIdx) moveCol.SetOrdinal(targetIdx);
    }
    private static string GetExcelColumnLetter(int columnNumber)
    {
        var result = "";
        while (columnNumber > 0)
        {
            columnNumber--;
            result = (char)('A' + columnNumber % 26) + result;
            columnNumber /= 26;
        }
        return result;
    }
}
