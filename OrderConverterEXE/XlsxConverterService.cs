using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OrderConverterEXE;

public class XlsxConverterService : BackgroundService
{
    private readonly ILogger<XlsxConverterService> _logger;
    private readonly Configuration _config;
    private readonly ConcurrentDictionary<string, DateTime> _processedFiles = new();

    public XlsxConverterService(ILogger<XlsxConverterService> logger, Configuration config)
    {
        _logger = logger;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        EnsureDirectories();

        _logger.LogInformation("监控间隔: {Interval} 秒", _config.CheckIntervalSeconds);
        _logger.LogInformation("程序运行中，按Ctrl+C 停止...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                ScanAndConvert();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "扫描和转换过程中出错");
            }

            await Task.Delay(TimeSpan.FromSeconds(_config.CheckIntervalSeconds), stoppingToken);
        }
    }

    private void EnsureDirectories()
    {
        var sourceDir = _config.GetSourcePath();
        var targetDir = _config.GetTargetPath();

        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(targetDir);

        _logger.LogInformation("源目录: {SourceDir}", Path.GetFullPath(sourceDir));
        _logger.LogInformation("目标目录: {TargetDir}", Path.GetFullPath(targetDir));
        _logger.LogInformation("模式: 递归扫描所有子目录，保持文件夹结构，转换后删除原文件");
    }

    private void ScanAndConvert()
    {
        var sourceDir = _config.GetSourcePath();
        if (!Directory.Exists(sourceDir))
        {
            _logger.LogWarning("源目录不存在: {SourceDir}", sourceDir);
            return;
        }

        // 同步目录结构
        SyncDirectoryStructure();

        // 递归查找所有 xlsx 文件
        var xlsxFiles = Directory.GetFiles(sourceDir, "*.xlsx", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(sourceDir, "*.XLSX", SearchOption.AllDirectories))
            .ToList();

        if (xlsxFiles.Count == 0)
        {
            return;
        }

        _logger.LogInformation("发现 {Count} 个 xlsx 文件", xlsxFiles.Count);

        // 按目录优先级排序：先处理 Quotation 目录，再处理 Orders 目录
        // 这样确保找货表在生成采购表之前已经被转换
        var sortedFiles = xlsxFiles.OrderBy(file =>
        {
            var relativePath = Path.GetRelativePath(sourceDir, file);
            if (relativePath.Contains("Quotation", StringComparison.OrdinalIgnoreCase))
                return 0; // Quotation 目录优先
            if (relativePath.Contains("Orders", StringComparison.OrdinalIgnoreCase))
                return 1; // Orders 目录其次
            return 2; // 其他目录最后
        }).ToList();

        // 收集所有需要删除的文件（等所有文件处理完后再删除）
        var filesToDelete = new List<(string FilePath, string RelativePath)>();

        foreach (var xlsxFile in sortedFiles)
        {
            try
            {
                // 再次检查文件是否存在（可能在之前的处理中已被删除）
                if (!File.Exists(xlsxFile))
                {
                    _logger.LogInformation("文件已被删除，跳过处理: {File}", xlsxFile);
                    continue;
                }

                var relativePath = Path.GetRelativePath(sourceDir, xlsxFile);
                _logger.LogInformation("准备处理文件: {File}, 相对路径: {RelativePath}", xlsxFile, relativePath);
                var (result, shouldDelete) = ConvertXlsxToCsv(xlsxFile, relativePath);
                if (!result)
                {
                    _logger.LogInformation("文件处理跳过（可能已处理）: {File}", xlsxFile);
                }
                else if (shouldDelete)
                {
                    // 记录需要删除的文件
                    filesToDelete.Add((xlsxFile, relativePath));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理文件时出错: {File}, 错误: {Error}", xlsxFile, ex.Message);
            }
        }

        // 所有文件处理完成后，统一删除源文件
        foreach (var (filePath, relativePath) in filesToDelete)
        {
            try
            {
                DeleteSourceFile(filePath, relativePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除源文件失败: {File}, 错误: {Error}", filePath, ex.Message);
            }
        }
    }

    private void SyncDirectoryStructure()
    {
        var sourceDir = _config.GetSourcePath();
        var targetDir = _config.GetTargetPath();

        if (!Directory.Exists(sourceDir)) return;

        try
        {
            foreach (var dir in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                var relativeDir = Path.GetRelativePath(sourceDir, dir);
                var targetSubDir = Path.Combine(targetDir, relativeDir);
                Directory.CreateDirectory(targetSubDir);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "同步目录结构时出错");
        }
    }

    private string GetSafeFilename(string filename)
    {
        var name = Path.GetFileNameWithoutExtension(filename);
        // 只保留字母、数字、中文、空格、连字符和下划线
        var safeName = Regex.Replace(name, @"[^\w\s\-_（）()]", "");
        return safeName.Replace(" ", "_");
    }

    /// <summary>
    /// 生成Orders目录的Excel文件名
    /// 格式：上上级目录名字 + 上级目录名字 + （源文件名中的年月日英文格式）+ "tracking & cost.xlsx"
    /// 例如：LG-Le Orders Jan23rd tracking & cost.xlsx
    /// </summary>
    private string GenerateOrdersFileName(string relativePath, string relativeDir, string sourceFileName)
    {
        // 从相对路径中提取目录信息
        // relativePath 格式：LG-Le\Orders\order_xxx.xlsx
        // relativeDir 格式：LG-Le\Orders

        var pathParts = relativeDir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        string parentParentDir = ""; // 上上级目录
        string parentDir = "";      // 上级目录

        if (pathParts.Count >= 2)
        {
            parentParentDir = pathParts[pathParts.Count - 2]; // 上上级目录
            parentDir = pathParts[pathParts.Count - 1];        // 上级目录
        }
        else if (pathParts.Count == 1)
        {
            parentDir = pathParts[0];
        }

        // 从源文件名中提取日期并转换为英文格式
        // 例如：order_20260123085324886_493386.xlsx -> 提取 20260123 -> 转换为 Jan23rd
        var date = ExtractDateFromFileName(sourceFileName);
        var dateStr = FormatDateToEnglish(date);

        // 组合文件名
        var fileName = $"{parentParentDir} {parentDir} {dateStr} tracking & cost.xlsx";

        // 清理文件名（移除多余空格，确保文件名合法）
        fileName = Regex.Replace(fileName, @"\s+", " ").Trim();

        _logger.LogInformation("生成Orders文件名 - 上上级目录: {ParentParentDir}, 上级目录: {ParentDir}, 源文件名: {SourceFileName}, 日期: {DateStr}, 文件名: {FileName}",
            parentParentDir, parentDir, sourceFileName, dateStr, fileName);

        return fileName;
    }

    /// <summary>
    /// 将日期转换为英文格式，例如：Jan16th, Feb1st, Mar2nd, Apr3rd
    /// </summary>
    private string FormatDateToEnglish(DateTime date)
    {
        var monthNames = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
        var month = monthNames[date.Month - 1];
        var day = date.Day;

        // 确定日期后缀
        string suffix;
        if (day >= 11 && day <= 13)
        {
            suffix = "th";
        }
        else
        {
            switch (day % 10)
            {
                case 1:
                    suffix = "st";
                    break;
                case 2:
                    suffix = "nd";
                    break;
                case 3:
                    suffix = "rd";
                    break;
                default:
                    suffix = "th";
                    break;
            }
        }

        return $"{month}{day}{suffix}";
    }

    /// <summary>
    /// 生成采购表文件名
    /// 格式：上上级目录名字 + 上级目录名字 + （源文件名中的年月日中文格式）+ "采购.xlsx"
    /// 例如：LG-Le Orders 2026年1月23日 采购.xlsx
    /// </summary>
    private string GeneratePurchaseTableFileName(string relativePath, string relativeDir, string sourceFileName)
    {
        // 从相对路径中提取目录信息
        // relativePath 格式：LG-Le\Orders\order_xxx.xlsx
        // relativeDir 格式：LG-Le\Orders

        var pathParts = relativeDir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        string parentParentDir = ""; // 上上级目录
        string parentDir = "";      // 上级目录

        if (pathParts.Count >= 2)
        {
            parentParentDir = pathParts[pathParts.Count - 2]; // 上上级目录
            parentDir = pathParts[pathParts.Count - 1];        // 上级目录
        }
        else if (pathParts.Count == 1)
        {
            parentDir = pathParts[0];
        }

        // 从源文件名中提取日期并转换为中文格式
        // 例如：order_20260123085324886_493386.xlsx -> 提取 20260123 -> 转换为 2026年1月23日
        var date = ExtractDateFromFileName(sourceFileName);
        var dateStr = FormatDateToChinese(date);

        // 组合文件名
        var fileName = $"{parentParentDir} {parentDir} {dateStr} 采购.xlsx";

        // 清理文件名（移除多余空格，确保文件名合法）
        fileName = Regex.Replace(fileName, @"\s+", " ").Trim();

        _logger.LogInformation("生成采购表文件名 - 上上级目录: {ParentParentDir}, 上级目录: {ParentDir}, 源文件名: {SourceFileName}, 日期: {DateStr}, 文件名: {FileName}",
            parentParentDir, parentDir, sourceFileName, dateStr, fileName);

        return fileName;
    }

    /// <summary>
    /// 从文件名中提取日期
    /// 例如：order_20260123085324886_493386.xlsx -> 提取 20260123 -> 返回 DateTime(2026, 1, 23)
    /// </summary>
    private DateTime ExtractDateFromFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            // 如果无法提取，使用当前日期
            _logger.LogWarning("源文件名为空，使用当前日期");
            return DateTime.Now;
        }

        // 移除文件扩展名
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

        // 尝试匹配日期格式：YYYYMMDD（8位数字）
        // 例如：order_20260123085324886_493386 -> 匹配 20260123
        var dateMatch = Regex.Match(nameWithoutExt, @"(\d{4})(\d{2})(\d{2})");

        if (dateMatch.Success)
        {
            try
            {
                var year = int.Parse(dateMatch.Groups[1].Value);
                var month = int.Parse(dateMatch.Groups[2].Value);
                var day = int.Parse(dateMatch.Groups[3].Value);

                // 验证日期是否有效
                if (month >= 1 && month <= 12 && day >= 1 && day <= 31)
                {
                    var date = new DateTime(year, month, day);
                    _logger.LogInformation("从文件名提取日期成功 - 文件名: {FileName}, 日期: {Date}", fileName, date.ToString("yyyy年MM月dd日"));
                    return date;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "解析文件名中的日期失败 - 文件名: {FileName}", fileName);
            }
        }

        // 如果无法提取，使用当前日期
        _logger.LogWarning("无法从文件名中提取日期，使用当前日期 - 文件名: {FileName}", fileName);
        return DateTime.Now;
    }

    /// <summary>
    /// 将日期转换为中文格式，例如：2026年1月17日
    /// </summary>
    private string FormatDateToChinese(DateTime date)
    {
        return $"{date.Year}年{date.Month}月{date.Day}日";
    }

    private (bool Success, bool ShouldDelete) ConvertXlsxToCsv(string xlsxFilePath, string relativePath)
    {
        if (!File.Exists(xlsxFilePath))
        {
            _logger.LogWarning("文件不存在: {Path}", xlsxFilePath);
            return (false, false);
        }

        var fileInfo = new FileInfo(xlsxFilePath);
        var fileKey = $"{relativePath}_{fileInfo.LastWriteTime:O}";

        // 检查是否已处理
        if (_processedFiles.TryGetValue(relativePath, out var lastWriteTime) &&
            lastWriteTime == fileInfo.LastWriteTime)
        {
            _logger.LogInformation("文件已处理过，跳过: {RelativePath} (最后修改时间: {LastWriteTime})", relativePath, lastWriteTime);
            return (false, false);
        }

        _logger.LogInformation("文件需要处理: {RelativePath} (最后修改时间: {CurrentWriteTime}, 已记录时间: {RecordedWriteTime})",
            relativePath, fileInfo.LastWriteTime,
            _processedFiles.TryGetValue(relativePath, out var recorded) ? recorded.ToString() : "无");

        try
        {
            _logger.LogInformation("开始转换文件: {RelativePath}", relativePath);

            var sourceDir = _config.GetSourcePath();
            var targetDir = _config.GetTargetPath();
            var relativeDir = Path.GetDirectoryName(relativePath) ?? "";
            var targetSubDir = Path.Combine(targetDir, relativeDir);
            Directory.CreateDirectory(targetSubDir);

            var baseName = GetSafeFilename(Path.GetFileName(xlsxFilePath));
            var isOrdersDir = relativePath.Contains("Orders", StringComparison.OrdinalIgnoreCase) ||
                             relativeDir.Contains("Orders", StringComparison.OrdinalIgnoreCase);

            // 尝试加载 Excel 文件，如果遇到图片问题则使用 OpenXML 直接读取
            XLWorkbook? workbook = null;
            try
            {
                // 直接加载，如果遇到图片问题会在内部处理
                workbook = new XLWorkbook(xlsxFilePath);
            }
            catch (ArgumentException ex) when (ex.Message.Contains("Picture names cannot be more than 31 characters") ||
                                               ex.Message.Contains("Picture"))
            {
                // 如果是因为图片问题，使用 OpenXML 直接读取数据，跳过图片
                _logger.LogWarning("文件包含图片问题，使用备用方法加载: {RelativePath}", relativePath);
                try
                {
                    workbook = LoadWorkbookWithoutPictures(xlsxFilePath);
                }
                catch (Exception ex2)
                {
                    _logger.LogError(ex2, "使用备用方法加载 Excel 文件失败: {RelativePath}", relativePath);
                    return (false, false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载 Excel 文件失败: {RelativePath}", relativePath);
                return (false, false);
            }

            if (workbook == null)
            {
                _logger.LogError("无法加载 Excel 文件: {RelativePath}", relativePath);
                return (false, false);
            }

            using (workbook)
            {
                var convertedFiles = new List<string>();

                foreach (var worksheet in workbook.Worksheets)
                {
                    try
                    {
                        // 跳过 WPS 内部 sheet
                        var rangeUsed = worksheet.RangeUsed();
                        if (rangeUsed == null ||
                            (worksheet.Name.Contains("WpsReserved", StringComparison.OrdinalIgnoreCase) ||
                             worksheet.Name.Contains("Reserved", StringComparison.OrdinalIgnoreCase)))
                        {
                            _logger.LogDebug("跳过内部 sheet: {SheetName}", worksheet.Name);
                            continue;
                        }

                        var safeSheetName = GetSafeFilename(worksheet.Name);
                        string outputFilename;
                        string? sourceFileName = null;

                        if (isOrdersDir)
                        {
                            // Orders目录：使用新格式，第一个文件带 onlyShip_ 前缀
                            sourceFileName = Path.GetFileName(xlsxFilePath);
                            var baseFileName = GenerateOrdersFileName(relativePath, relativeDir, sourceFileName);
                            outputFilename = $"onlyShip_{baseFileName}";
                        }
                        else
                        {
                            // 非Orders目录：保持原有逻辑
                            var prefix = "";
                            outputFilename = string.IsNullOrEmpty(safeSheetName)
                                ? $"{prefix}{baseName}_sheet1.csv"
                                : $"{prefix}{baseName}_{safeSheetName}.csv";
                        }

                        var outputFile = Path.Combine(targetSubDir, outputFilename);

                        if (File.Exists(outputFile))
                        {
                            _logger.LogInformation("覆盖已存在的文件: {File}", Path.Combine(relativeDir, outputFilename));
                        }

                        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
                        var lastCol = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;

                        if (isOrdersDir)
                        {
                            // 生成第一个文件（带 onlyShip_ 前缀）
                            ProcessOrdersFile(worksheet, outputFile, safeSheetName);
                            convertedFiles.Add(outputFile);
                            _logger.LogInformation("✓ 已转换: {File} ({Rows} 行, {Cols} 列)",
                                Path.Combine(relativeDir, outputFilename),
                                lastRow,
                                lastCol);

                            // 生成第二个文件（不带 onlyShip_ 前缀）
                            // 新格式：上上级目录名字 + 上级目录名字 + （源文件名中的年月日英文格式）+ "tracking & cost.xlsx"
                            // 例如：LG-Le Orders Jan23rd tracking & cost.xlsx
                            if (sourceFileName == null)
                            {
                                sourceFileName = Path.GetFileName(xlsxFilePath);
                            }
                            var outputFilename2 = GenerateOrdersFileName(relativePath, relativeDir, sourceFileName);
                            var outputFile2 = Path.Combine(targetSubDir, outputFilename2);
                            ProcessOrdersFileForTotalToFr(worksheet, outputFile2, safeSheetName);
                            convertedFiles.Add(outputFile2);
                            _logger.LogInformation("✓ 已转换: {File} ({Rows} 行, {Cols} 列) - 使用Total to列",
                                Path.Combine(relativeDir, outputFilename2),
                                lastRow,
                                lastCol);

                            // 如果是 LG-Le\Orders 目录，生成采购表
                            var isLgLeOrders = relativePath.Contains("LG-Le", StringComparison.OrdinalIgnoreCase) &&
                                              (relativePath.Contains("Orders", StringComparison.OrdinalIgnoreCase) ||
                                               relativeDir.Contains("Orders", StringComparison.OrdinalIgnoreCase));

                            _logger.LogInformation("检查采购表生成条件 - relativePath: {RelativePath}, relativeDir: {RelativeDir}, isLgLeOrders: {IsLgLeOrders}",
                                relativePath, relativeDir, isLgLeOrders);

                            if (isLgLeOrders)
                            {
                                try
                                {
                                    _logger.LogInformation("开始生成采购表 - 源文件: {SourceFile}", xlsxFilePath);
                                    // 新格式：上上级目录名字 + 上级目录名字 + （源文件名中的年月日中文格式）+ "采购.xlsx"
                                    // 例如：LG-Le Orders 2026年1月23日 采购.xlsx
                                    if (sourceFileName == null)
                                    {
                                        sourceFileName = Path.GetFileName(xlsxFilePath);
                                    }
                                    var purchaseFileName = GeneratePurchaseTableFileName(relativePath, relativeDir, sourceFileName);
                                    var purchaseFilePath = Path.Combine(targetSubDir, purchaseFileName);
                                    _logger.LogInformation("采购表文件路径: {PurchaseFilePath}", purchaseFilePath);

                                    GeneratePurchaseTable(worksheet, purchaseFilePath, baseName);

                                    if (File.Exists(purchaseFilePath))
                                    {
                                        convertedFiles.Add(purchaseFilePath);
                                        _logger.LogInformation("✓ 已生成采购表: {File}", Path.Combine(relativeDir, purchaseFileName));
                                    }
                                    else
                                    {
                                        _logger.LogWarning("采购表文件生成失败，文件不存在: {File}", purchaseFilePath);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "生成采购表失败: {File}, 错误: {Error}", xlsxFilePath, ex.Message);
                                }
                            }
                            else
                            {
                                _logger.LogInformation("跳过采购表生成 - 不是LG-Le\\Orders目录");
                            }
                        }
                        else
                        {
                            SaveAsCsv(worksheet, outputFile);
                            _logger.LogInformation("✓ 已转换: {File} ({Rows} 行, {Cols} 列)",
                                Path.Combine(relativeDir, outputFilename),
                                lastRow,
                                lastCol);
                        }

                        if (!isOrdersDir)
                        {
                            convertedFiles.Add(outputFile);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "转换 sheet '{SheetName}' 时出错", worksheet.Name);
                    }
                }

                if (convertedFiles.Count > 0)
                {
                    // 标记为已处理，但不立即删除文件
                    _processedFiles[relativePath] = fileInfo.LastWriteTime;
                    return (true, true); // 返回成功，并标记需要删除
                }

                return (false, false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "转换文件时出错: {RelativePath}", relativePath);
            return (false, false);
        }
    }

    private void SaveAsCsv(IXLWorksheet worksheet, string outputFile)
    {
        using var writer = new StreamWriter(outputFile, false, new UTF8Encoding(true)); // UTF-8 with BOM

        var rangeUsed = worksheet.RangeUsed();
        if (rangeUsed == null) return;

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
        var lastCol = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;

        // 写入表头
        var headers = new List<string>();
        for (int col = 1; col <= lastCol; col++)
        {
            var cellValue = worksheet.Cell(1, col).Value.ToString();
            headers.Add(cellValue);
        }
        writer.WriteLine(string.Join(",", headers.Select(h => EscapeCsvField(h))));

        // 写入数据
        for (int row = 2; row <= lastRow; row++)
        {
            var values = new List<string>();
            for (int col = 1; col <= lastCol; col++)
            {
                var cellValue = worksheet.Cell(row, col).Value.ToString();
                values.Add(cellValue);
            }
            writer.WriteLine(string.Join(",", values.Select(v => EscapeCsvField(v))));
        }
    }

    private string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field)) return "";

        // 如果包含逗号、引号或换行符，需要用引号括起来
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }

    private void ProcessOrdersFile(IXLWorksheet worksheet, string outputFile, string sheetName)
    {
        // 读取数据到 DataTable
        var data = ReadWorksheetToDataTable(worksheet);

        // 处理 Orders 目录的特殊逻辑
        ProcessOrdersData(data);

        // 保存为 Excel 格式
        SaveAsExcel(data, outputFile, sheetName);
    }

    private void ProcessOrdersFileForTotalToFr(IXLWorksheet worksheet, string outputFile, string sheetName)
    {
        // 读取数据到 DataTable
        var data = ReadWorksheetToDataTable(worksheet);

        // 处理 Orders 目录的特殊逻辑（使用 Total to FR 列）
        ProcessOrdersDataForTotalToFr(data);

        // 保存为 Excel 格式
        SaveAsExcel(data, outputFile, sheetName);
    }

    private System.Data.DataTable ReadWorksheetToDataTable(IXLWorksheet worksheet)
    {
        var dataTable = new System.Data.DataTable();

        var rangeUsed = worksheet.RangeUsed();
        if (rangeUsed == null) return dataTable;

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
        var lastCol = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;

        // 读取表头
        for (int col = 1; col <= lastCol; col++)
        {
            var headerValue = worksheet.Cell(1, col).Value.ToString();
            if (string.IsNullOrEmpty(headerValue))
                headerValue = $"Column{col}";
            dataTable.Columns.Add(headerValue, typeof(string));
        }

        // 读取数据
        for (int row = 2; row <= lastRow; row++)
        {
            var dataRow = dataTable.NewRow();
            for (int col = 1; col <= lastCol; col++)
            {
                var cellValue = worksheet.Cell(row, col).Value.ToString();
                dataRow[col - 1] = cellValue;
            }
            dataTable.Rows.Add(dataRow);
        }

        return dataTable;
    }

    private void ProcessOrdersData(System.Data.DataTable data)
    {
        if (data.Rows.Count == 0) return;

        // 0. 修复缺失的商品SKU值
        FixMissingProductSku(data);

        // 1. 删除不需要的列
        RemoveUnnecessaryColumns(data);

        // 2. 添加 SKU_Quotation 列
        AddSkuQuotationColumn(data);

        // 2. 添加 QTY_Merged 列
        AddQtyMergedColumn(data);

        // 3. 添加 cost 和 upsell 列
        AddCostAndUpsellColumns(data);

        // 4. 添加 sum 行
        AddSumRow(data);
    }

    private void ProcessOrdersDataForTotalToFr(System.Data.DataTable data)
    {
        if (data.Rows.Count == 0) return;

        // 0. 修复缺失的商品SKU值
        FixMissingProductSku(data);

        // 1. 删除不需要的列
        RemoveUnnecessaryColumns(data);

        // 2. 添加 SKU_Quotation 列
        AddSkuQuotationColumn(data);

        // 3. 添加 QTY_Merged 列
        AddQtyMergedColumn(data);

        // 4. 添加 cost 和 upsell 列（使用 Total to FR 列）
        AddCostAndUpsellColumnsForTotalToFr(data);

        // 5. 添加 sum 行
        AddSumRow(data);
    }

    /// <summary>
    /// 修复缺失的商品SKU值：根据Item name列的值，找到Item name相同且商品SKU不为空的行，用其商品SKU值填充当前行
    /// </summary>
    private void FixMissingProductSku(System.Data.DataTable data)
    {
        if (data.Rows.Count == 0) return;

        // 记录所有列名，便于调试
        var allColumns = data.Columns.Cast<System.Data.DataColumn>()
            .Select(c => c.ColumnName)
            .ToList();
        _logger.LogInformation("修复商品SKU - 数据表所有列名: {Columns}", string.Join(", ", allColumns));

        // 查找商品SKU列和Item name列
        string? productSkuColumn = null;
        string? itemNameColumn = null;

        foreach (System.Data.DataColumn col in data.Columns)
        {
            var colName = col.ColumnName.Trim();
            if (colName.Contains("商品SKU", StringComparison.OrdinalIgnoreCase))
            {
                productSkuColumn = col.ColumnName;
                _logger.LogInformation("找到商品SKU列: {ColumnName}", col.ColumnName);
            }
            // 更灵活的Item name列匹配：支持"Item name"、"Item Name"、"Item Name"等变体
            if (colName.Contains("Item", StringComparison.OrdinalIgnoreCase) &&
                colName.Contains("name", StringComparison.OrdinalIgnoreCase))
            {
                itemNameColumn = col.ColumnName;
                _logger.LogInformation("找到Item name列: {ColumnName}", col.ColumnName);
            }
        }

        if (productSkuColumn == null)
        {
            _logger.LogWarning("未找到商品SKU列，跳过修复缺失商品SKU值 - 可用列: {Columns}", string.Join(", ", allColumns));
            return;
        }

        if (itemNameColumn == null)
        {
            _logger.LogWarning("未找到Item name列，跳过修复缺失商品SKU值 - 可用列: {Columns}", string.Join(", ", allColumns));
            return;
        }

        _logger.LogInformation("开始修复缺失的商品SKU值 - 商品SKU列: {ProductSkuColumn}, Item name列: {ItemNameColumn}",
            productSkuColumn, itemNameColumn);

        // 构建Item name到商品SKU的映射（只包含商品SKU不为空的行）
        // 使用Item name的主要部分（去掉颜色和尺寸信息）作为key
        var itemNameToSkuMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (System.Data.DataRow row in data.Rows)
        {
            var itemName = row[itemNameColumn]?.ToString()?.Trim() ?? "";
            var productSku = row[productSkuColumn]?.ToString()?.Trim() ?? "";

            // 如果Item name和商品SKU都不为空，添加到映射中
            if (!string.IsNullOrWhiteSpace(itemName) && !string.IsNullOrWhiteSpace(productSku))
            {
                // 提取Item name的主要部分（去掉颜色和尺寸信息）
                var itemNameMainPart = ExtractItemNameMainPart(itemName);

                // 如果已存在，保留第一个（或者可以更新，这里保留第一个）
                if (!itemNameToSkuMap.ContainsKey(itemNameMainPart))
                {
                    itemNameToSkuMap[itemNameMainPart] = productSku;
                    _logger.LogDebug("添加映射 - Item name主要部分: {ItemNameMainPart}, 商品SKU: {ProductSku}, 原始Item name: {ItemName}",
                        itemNameMainPart, productSku, itemName);
                }
            }
        }

        _logger.LogInformation("构建Item name到商品SKU映射完成 - 映射数量: {Count}", itemNameToSkuMap.Count);

        // 如果映射为空，记录警告
        if (itemNameToSkuMap.Count == 0)
        {
            _logger.LogWarning("Item name到商品SKU映射为空，无法修复缺失的商品SKU值。可能原因：源文件中所有行的商品SKU都为空。");

            // 统计一下有多少行有Item name但没有商品SKU
            int rowsWithItemNameButNoSku = 0;
            foreach (System.Data.DataRow row in data.Rows)
            {
                var productSku = row[productSkuColumn]?.ToString()?.Trim() ?? "";
                var itemName = row[itemNameColumn]?.ToString()?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(productSku) && !string.IsNullOrWhiteSpace(itemName))
                {
                    rowsWithItemNameButNoSku++;
                }
            }
            if (rowsWithItemNameButNoSku > 0)
            {
                _logger.LogWarning("发现 {Count} 行有Item name但商品SKU为空，但映射表也为空，无法修复", rowsWithItemNameButNoSku);
            }
            return;
        }

        // 统计缺失商品SKU的行数
        int missingSkuCount = 0;
        int missingSkuWithItemNameCount = 0;

        // 修复缺失的商品SKU值
        int fixedCount = 0;
        int failedCount = 0;
        foreach (System.Data.DataRow row in data.Rows)
        {
            var productSku = row[productSkuColumn]?.ToString()?.Trim() ?? "";
            var itemName = row[itemNameColumn]?.ToString()?.Trim() ?? "";

            // 统计缺失商品SKU的行
            if (string.IsNullOrWhiteSpace(productSku))
            {
                missingSkuCount++;
                if (!string.IsNullOrWhiteSpace(itemName))
                {
                    missingSkuWithItemNameCount++;
                }
            }

            // 如果商品SKU为空但Item name不为空，尝试从映射中获取
            if (string.IsNullOrWhiteSpace(productSku) && !string.IsNullOrWhiteSpace(itemName))
            {
                // 提取Item name的主要部分（去掉颜色和尺寸信息）
                var itemNameMainPart = ExtractItemNameMainPart(itemName);

                if (itemNameToSkuMap.TryGetValue(itemNameMainPart, out var mappedSku))
                {
                    row[productSkuColumn] = mappedSku;
                    fixedCount++;
                    _logger.LogInformation("修复商品SKU值 - Item name主要部分: {ItemNameMainPart}, 商品SKU: {ProductSku}, 原始Item name: {ItemName}",
                        itemNameMainPart, mappedSku, itemName);
                }
                else
                {
                    failedCount++;
                    _logger.LogWarning("无法修复商品SKU值 - Item name主要部分: {ItemNameMainPart} 在映射中未找到对应的商品SKU, 原始Item name: {ItemName}",
                        itemNameMainPart, itemName);
                }
            }
        }

        _logger.LogInformation("商品SKU修复统计 - 缺失商品SKU的行数: {MissingCount}, 有Item name的缺失行数: {MissingWithItemNameCount}, 成功修复行数: {FixedCount}, 修复失败行数: {FailedCount}, 映射数量: {MapCount}",
            missingSkuCount, missingSkuWithItemNameCount, fixedCount, failedCount, itemNameToSkuMap.Count);

        if (fixedCount > 0)
        {
            _logger.LogInformation("修复缺失的商品SKU值完成 - 修复行数: {FixedCount}", fixedCount);
        }
        else if (missingSkuCount > 0)
        {
            _logger.LogWarning("存在缺失商品SKU的行，但无法修复 - 缺失行数: {MissingCount}, 有Item name的缺失行数: {MissingWithItemNameCount}",
                missingSkuCount, missingSkuWithItemNameCount);
        }
        else
        {
            _logger.LogDebug("无需修复商品SKU值（所有行的商品SKU都已存在）");
        }
    }

    private void RemoveUnnecessaryColumns(System.Data.DataTable data)
    {
        // 要删除的列名列表（不区分大小写）
        var columnsToRemove = new[] { "Item cost", "Shipping cost" };
        var removedColumns = new List<string>();

        foreach (var columnName in columnsToRemove)
        {
            // 查找匹配的列（不区分大小写）
            System.Data.DataColumn? columnToRemove = null;
            foreach (System.Data.DataColumn col in data.Columns)
            {
                if (col.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase))
                {
                    columnToRemove = col;
                    break;
                }
            }

            if (columnToRemove != null)
            {
                data.Columns.Remove(columnToRemove);
                removedColumns.Add(columnToRemove.ColumnName);
            }
        }

        if (removedColumns.Count > 0)
        {
            _logger.LogInformation("已删除列: {Columns}", string.Join(", ", removedColumns));
        }
    }

    private void AddSkuQuotationColumn(System.Data.DataTable data)
    {
        // 查找商品SKU列
        string? productSkuColumn = null;
        foreach (System.Data.DataColumn col in data.Columns)
        {
            var colName = col.ColumnName.Trim();
            if (colName.Contains("商品SKU", StringComparison.OrdinalIgnoreCase))
            {
                productSkuColumn = col.ColumnName;
                break;
            }
        }

        // 查找SKU列（不包含"商品"的SKU列）
        string? skuColumn = null;
        foreach (System.Data.DataColumn col in data.Columns)
        {
            var colName = col.ColumnName.Trim();
            if (colName.Equals("SKU", StringComparison.OrdinalIgnoreCase) ||
                (colName.Contains("SKU", StringComparison.OrdinalIgnoreCase) &&
                 !colName.Contains("商品", StringComparison.OrdinalIgnoreCase) &&
                 !colName.Contains("QUOTATION", StringComparison.OrdinalIgnoreCase)))
            {
                skuColumn = col.ColumnName;
                break;
            }
        }

        // 如果找不到商品SKU列，尝试查找其他包含SKU的列作为备用
        if (productSkuColumn == null)
        {
            foreach (System.Data.DataColumn col in data.Columns)
            {
                var colName = col.ColumnName.Trim().ToUpper();
                if (colName.Contains("SKU") && !colName.Contains("QUOTATION"))
                {
                    productSkuColumn = col.ColumnName;
                    break;
                }
            }
        }

        if (productSkuColumn == null)
        {
            _logger.LogWarning("未找到商品SKU列，跳过添加SKU_Quotation列");
            return;
        }

        // 如果存在SKU列，用商品SKU列的值替换SKU列的值
        if (skuColumn != null && !skuColumn.Equals(productSkuColumn, StringComparison.OrdinalIgnoreCase))
        {
            int replacedCount = 0;
            int emptyCount = 0;
            foreach (System.Data.DataRow row in data.Rows)
            {
                var productSkuValue = row[productSkuColumn]?.ToString()?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(productSkuValue))
                {
                    emptyCount++;
                    _logger.LogWarning("商品SKU值为空，无法替换SKU列 - 行索引: {RowIndex}", data.Rows.IndexOf(row));
                }
                else
                {
                    replacedCount++;
                }
                row[skuColumn] = productSkuValue;
            }
            _logger.LogInformation("已用商品SKU列的值替换SKU列的值 - 成功替换: {ReplacedCount}, 空值: {EmptyCount}", replacedCount, emptyCount);
        }

        // 添加 SKU_Quotation 列
        if (!data.Columns.Contains("SKU_Quotation"))
        {
            data.Columns.Add("SKU_Quotation", typeof(string));
        }

        // 从商品SKU列提取 SKU 前缀
        int emptySkuCount = 0;
        foreach (System.Data.DataRow row in data.Rows)
        {
            var skuValue = row[productSkuColumn]?.ToString()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(skuValue))
            {
                emptySkuCount++;
                _logger.LogWarning("商品SKU列为空，无法提取SKU前缀 - 行索引: {RowIndex}", data.Rows.IndexOf(row));
            }
            row["SKU_Quotation"] = ExtractSkuPrefix(skuValue);
        }

        if (emptySkuCount > 0)
        {
            _logger.LogWarning("SKU_Quotation列生成完成，但发现 {EmptyCount} 行商品SKU为空", emptySkuCount);
        }

        // 重新排列列顺序：将SKU_Quotation列移到SKU列的右边
        // 如果SKU列存在，则放在SKU列右边；否则放在商品SKU列右边
        var targetColumn = skuColumn ?? productSkuColumn;
        ReorderColumn(data, "SKU_Quotation", targetColumn, after: true);

        _logger.LogInformation("已添加SKU_Quotation列（从'{ProductSkuColumn}'列提取，位于{TargetColumn}列右边）",
            productSkuColumn, targetColumn);
    }

    private string ExtractSkuPrefix(string skuValue)
    {
        if (string.IsNullOrWhiteSpace(skuValue)) return "";

        var parts = skuValue.Split('-');
        if (parts.Length >= 2)
        {
            return $"{parts[0]}-{parts[1]}";
        }
        else if (parts.Length == 1)
        {
            return parts[0];
        }

        return "";
    }

    private void AddQtyMergedColumn(System.Data.DataTable data)
    {
        // 查找 Order# 列和 QTY 列
        string? orderColumn = null;
        string? qtyColumn = null;

        foreach (System.Data.DataColumn col in data.Columns)
        {
            var colName = col.ColumnName.Trim();
            if ((colName.Contains("Order#", StringComparison.OrdinalIgnoreCase) ||
                 (colName.Contains("Order", StringComparison.OrdinalIgnoreCase) && colName.Contains("#"))) &&
                orderColumn == null)
            {
                orderColumn = col.ColumnName;
            }

            if (colName.Contains("QTY", StringComparison.OrdinalIgnoreCase) &&
                !colName.Contains("MERGED", StringComparison.OrdinalIgnoreCase) &&
                qtyColumn == null)
            {
                qtyColumn = col.ColumnName;
            }
        }

        if (orderColumn == null || qtyColumn == null)
        {
            var missing = new List<string>();
            if (orderColumn == null) missing.Add("Order#");
            if (qtyColumn == null) missing.Add("QTY");
            _logger.LogWarning("未找到{Missing}列，跳过添加QTY_Merged列", string.Join("、", missing));
            return;
        }

        // 添加 QTY_Merged 列
        if (!data.Columns.Contains("QTY_Merged"))
        {
            data.Columns.Add("QTY_Merged", typeof(string));
        }

        // 按 Order# 和 SKU_Quotation 分组计算
        var groups = data.Rows.Cast<System.Data.DataRow>()
            .GroupBy(row => new
            {
                Order = row[orderColumn]?.ToString() ?? "",
                SkuQuotation = row["SKU_Quotation"]?.ToString() ?? ""
            })
            .ToList();

        foreach (var group in groups)
        {
            var rows = group.ToList();
            var totalQty = rows.Sum(r =>
            {
                if (double.TryParse(r[qtyColumn]?.ToString(), out var qty))
                    return (int)qty;
                return 0;
            });

            // 只在第一行显示总和
            if (rows.Count > 0)
            {
                rows[0]["QTY_Merged"] = totalQty > 0 ? totalQty.ToString() : "";
            }
        }

        // 重新排列列顺序
        ReorderColumn(data, "QTY_Merged", qtyColumn, after: true);

        _logger.LogInformation("已添加QTY_Merged列（按Order#和SKU_Quotation分组累加QTY，位于QTY列右边）");
    }

    private void AddCostAndUpsellColumns(System.Data.DataTable data)
    {
        // 查找 Country 列和 Order# 列
        string? countryColumn = null;
        string? orderColumn = null;

        foreach (System.Data.DataColumn col in data.Columns)
        {
            var colName = col.ColumnName.Trim();
            if ((colName.Contains("Country", StringComparison.OrdinalIgnoreCase) ||
                 colName.Contains("国家", StringComparison.OrdinalIgnoreCase)) &&
                countryColumn == null)
            {
                countryColumn = col.ColumnName;
            }

            if ((colName.Contains("Order#", StringComparison.OrdinalIgnoreCase) ||
                 (colName.Contains("Order", StringComparison.OrdinalIgnoreCase) && colName.Contains("#"))) &&
                orderColumn == null)
            {
                orderColumn = col.ColumnName;
            }
        }

        if (countryColumn == null || orderColumn == null)
        {
            if (countryColumn == null)
                _logger.LogWarning("未找到Country列，跳过添加cost和upsell列");
            return;
        }

        // 添加 cost 和 upsell 列
        if (!data.Columns.Contains("cost"))
        {
            data.Columns.Add("cost", typeof(string));
        }
        if (!data.Columns.Contains("upsell"))
        {
            data.Columns.Add("upsell", typeof(string));
        }

        // 加载报价表
        var quotationData = LoadQuotationData();
        var upsellQuotationData = LoadUpsellQuotationData();

        // 按 Order# 分组处理
        var orderGroups = data.Rows.Cast<System.Data.DataRow>()
            .GroupBy(row => row[orderColumn]?.ToString() ?? "")
            .ToList();

        foreach (var orderGroup in orderGroups)
        {
            var orderRows = orderGroup.ToList();
            var rowsWithQty = orderRows
                .Select((row, idx) => new { Row = row, Index = idx, Qty = ParseQtyMerged(row["QTY_Merged"]?.ToString()) })
                .Where(x => x.Qty > 0)
                .ToList();

            if (rowsWithQty.Count == 0) continue;

            // 找到 QTY_Merged 最大的行（设置 cost）
            var maxQtyRow = rowsWithQty.OrderByDescending(x => x.Qty).First();
            SetCostValue(maxQtyRow.Row, quotationData, countryColumn);

            // 为其他行设置 upsell
            foreach (var otherRow in rowsWithQty.Where(x => x.Index != maxQtyRow.Index))
            {
                SetUpsellValue(otherRow.Row, upsellQuotationData, countryColumn);
            }
        }

        // 重新排列列顺序
        ReorderColumn(data, "cost", countryColumn, after: false);
        ReorderColumn(data, "upsell", "cost", after: true);

        _logger.LogInformation("已添加cost和upsell列（位于Country列左边）");
    }

    private void AddCostAndUpsellColumnsForTotalToFr(System.Data.DataTable data)
    {
        // 查找 Country 列和 Order# 列
        string? countryColumn = null;
        string? orderColumn = null;

        foreach (System.Data.DataColumn col in data.Columns)
        {
            var colName = col.ColumnName.Trim();
            if ((colName.Contains("Country", StringComparison.OrdinalIgnoreCase) ||
                 colName.Contains("国家", StringComparison.OrdinalIgnoreCase)) &&
                countryColumn == null)
            {
                countryColumn = col.ColumnName;
            }

            if ((colName.Contains("Order#", StringComparison.OrdinalIgnoreCase) ||
                 (colName.Contains("Order", StringComparison.OrdinalIgnoreCase) && colName.Contains("#"))) &&
                orderColumn == null)
            {
                orderColumn = col.ColumnName;
            }
        }

        if (countryColumn == null || orderColumn == null)
        {
            if (countryColumn == null)
                _logger.LogWarning("未找到Country列，跳过添加cost和upsell列");
            return;
        }

        // 添加 cost 和 upsell 列
        if (!data.Columns.Contains("cost"))
        {
            data.Columns.Add("cost", typeof(string));
        }
        if (!data.Columns.Contains("upsell"))
        {
            data.Columns.Add("upsell", typeof(string));
        }

        // 加载报价表
        var quotationData = LoadQuotationData();
        var upsellQuotationData = LoadUpsellQuotationData();

        // 按 Order# 分组处理
        var orderGroups = data.Rows.Cast<System.Data.DataRow>()
            .GroupBy(row => row[orderColumn]?.ToString() ?? "")
            .ToList();

        foreach (var orderGroup in orderGroups)
        {
            var orderRows = orderGroup.ToList();
            var rowsWithQty = orderRows
                .Select((row, idx) => new { Row = row, Index = idx, Qty = ParseQtyMerged(row["QTY_Merged"]?.ToString()) })
                .Where(x => x.Qty > 0)
                .ToList();

            if (rowsWithQty.Count == 0) continue;

            // 找到 QTY_Merged 最大的行（设置 cost）
            var maxQtyRow = rowsWithQty.OrderByDescending(x => x.Qty).First();
            SetCostValueByTotalToFr(maxQtyRow.Row, quotationData, countryColumn);

            // 为其他行设置 upsell
            foreach (var otherRow in rowsWithQty.Where(x => x.Index != maxQtyRow.Index))
            {
                SetUpsellValueByTotalToFr(otherRow.Row, upsellQuotationData, countryColumn);
            }
        }

        // 重新排列列顺序
        ReorderColumn(data, "cost", countryColumn, after: false);
        ReorderColumn(data, "upsell", "cost", after: true);

        _logger.LogInformation("已添加cost和upsell列（使用Total to列，位于Country列左边）");
    }

    private int ParseQtyMerged(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        if (int.TryParse(value, out var qty)) return qty;
        return 0;
    }

    private void SetCostValue(System.Data.DataRow row, System.Data.DataTable? quotationData, string countryColumn)
    {
        if (quotationData == null)
        {
            row["cost"] = "报价表文件不存在";
            return;
        }

        var skuQuotation = row["SKU_Quotation"]?.ToString()?.Trim() ?? "";
        var qtyMerged = ParseQtyMerged(row["QTY_Merged"]?.ToString());
        var country = row[countryColumn]?.ToString()?.Trim()?.ToUpper() ?? "";

        if (string.IsNullOrEmpty(skuQuotation) || string.IsNullOrEmpty(country) || qtyMerged == 0)
        {
            return;
        }

        var cost = FindQuotationValue(quotationData, skuQuotation, qtyMerged, country);
        row["cost"] = cost ?? "未找到匹配的数据";
    }

    private void SetUpsellValue(System.Data.DataRow row, System.Data.DataTable? upsellQuotationData, string countryColumn)
    {
        if (upsellQuotationData == null)
        {
            row["upsell"] = "upsell报价表文件不存在";
            return;
        }

        var skuQuotation = row["SKU_Quotation"]?.ToString()?.Trim() ?? "";
        var qtyMerged = ParseQtyMerged(row["QTY_Merged"]?.ToString());
        var country = row[countryColumn]?.ToString()?.Trim()?.ToUpper() ?? "";

        if (string.IsNullOrEmpty(skuQuotation) || string.IsNullOrEmpty(country) || qtyMerged == 0)
        {
            return;
        }

        var upsell = FindQuotationValue(upsellQuotationData, skuQuotation, qtyMerged, country);
        row["upsell"] = upsell ?? "未找到匹配的数据";
    }

    private void SetCostValueByTotalToFr(System.Data.DataRow row, System.Data.DataTable? quotationData, string countryColumn)
    {
        if (quotationData == null)
        {
            row["cost"] = "报价表文件不存在";
            return;
        }

        var skuQuotation = row["SKU_Quotation"]?.ToString()?.Trim() ?? "";
        var qtyMerged = ParseQtyMerged(row["QTY_Merged"]?.ToString());
        var country = row[countryColumn]?.ToString()?.Trim() ?? "";

        if (string.IsNullOrEmpty(skuQuotation) || string.IsNullOrEmpty(country) || qtyMerged == 0)
        {
            return;
        }

        var cost = FindQuotationValueByTotalToFr(quotationData, skuQuotation, qtyMerged, country);
        row["cost"] = cost ?? "未找到匹配的数据";
    }

    private void SetUpsellValueByTotalToFr(System.Data.DataRow row, System.Data.DataTable? upsellQuotationData, string countryColumn)
    {
        if (upsellQuotationData == null)
        {
            row["upsell"] = "upsell报价表文件不存在";
            return;
        }

        var skuQuotation = row["SKU_Quotation"]?.ToString()?.Trim() ?? "";
        var qtyMerged = ParseQtyMerged(row["QTY_Merged"]?.ToString());
        var country = row[countryColumn]?.ToString()?.Trim() ?? "";

        if (string.IsNullOrEmpty(skuQuotation) || string.IsNullOrEmpty(country) || qtyMerged == 0)
        {
            return;
        }

        var upsell = FindQuotationValueByTotalToFr(upsellQuotationData, skuQuotation, qtyMerged, country);
        row["upsell"] = upsell ?? "未找到匹配的数据";
    }

    private string? FindQuotationValueByTotalToFr(System.Data.DataTable quotationData, string skuQuotation, int qty, string countryCode)
    {
        // 查找 SKU 列
        System.Data.DataColumn? skuColumn = null;
        foreach (System.Data.DataColumn col in quotationData.Columns)
        {
            if (col.ColumnName.ToUpper().Contains("SKU"))
            {
                skuColumn = col;
                break;
            }
        }

        if (skuColumn == null) return null;

        // 查找匹配的 SKU 行
        var matchingRows = quotationData.Rows.Cast<System.Data.DataRow>()
            .Where(row => (row[skuColumn!]?.ToString()?.Trim() ?? "") == skuQuotation)
            .ToList();

        if (matchingRows.Count == 0) return null;

        // 查找 QTY 列
        var qtyColumns = quotationData.Columns.Cast<System.Data.DataColumn>()
            .Where(col => col.ColumnName.ToUpper().Contains("QTY") && !col.ColumnName.ToUpper().Contains("MERGED"))
            .ToList();

        // 查找 "Total to {countryCode}" 列（不区分大小写）
        var totalToColumnName = $"Total to {countryCode}";
        System.Data.DataColumn? totalToColumn = null;
        foreach (System.Data.DataColumn col in quotationData.Columns)
        {
            if (col.ColumnName.Trim().Equals(totalToColumnName, StringComparison.OrdinalIgnoreCase))
            {
                totalToColumn = col;
                break;
            }
        }

        if (totalToColumn == null) return null;

        // 查找匹配 QTY 的行
        foreach (var row in matchingRows)
        {
            foreach (var qtyCol in qtyColumns)
            {
                if (int.TryParse(row[qtyCol]?.ToString(), out var rowQty) && rowQty == qty)
                {
                    var value = row[totalToColumn]?.ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }
        }

        return null;
    }

    private string? FindQuotationValue(System.Data.DataTable quotationData, string skuQuotation, int qty, string countryCode)
    {
        // 查找 SKU 列
        System.Data.DataColumn? skuColumn = null;
        foreach (System.Data.DataColumn col in quotationData.Columns)
        {
            if (col.ColumnName.ToUpper().Contains("SKU"))
            {
                skuColumn = col;
                break;
            }
        }

        if (skuColumn == null) return null;

        // 查找匹配的 SKU 行
        var matchingRows = quotationData.Rows.Cast<System.Data.DataRow>()
            .Where(row => (row[skuColumn!]?.ToString()?.Trim() ?? "") == skuQuotation)
            .ToList();

        if (matchingRows.Count == 0) return null;

        // 查找 QTY 列
        var qtyColumns = quotationData.Columns.Cast<System.Data.DataColumn>()
            .Where(col => col.ColumnName.ToUpper().Contains("QTY") && !col.ColumnName.ToUpper().Contains("MERGED"))
            .ToList();

        // 查找以 "Ship to" 开头且包含国家代码的列（不区分大小写）
        // 例如：Country=FR 匹配 "Ship to FR(6-11 working days )"
        System.Data.DataColumn? shipToColumn = null;
        var countryCodeUpper = countryCode.ToUpper();
        foreach (System.Data.DataColumn col in quotationData.Columns)
        {
            var colNameUpper = col.ColumnName.Trim().ToUpper();
            if (colNameUpper.StartsWith("SHIP TO", StringComparison.OrdinalIgnoreCase) &&
                colNameUpper.Contains(countryCodeUpper))
            {
                shipToColumn = col;
                break;
            }
        }

        if (shipToColumn == null) return null;

        // 查找匹配 QTY 的行
        foreach (var row in matchingRows)
        {
            foreach (var qtyCol in qtyColumns)
            {
                if (int.TryParse(row[qtyCol]?.ToString(), out var rowQty) && rowQty == qty)
                {
                    var value = row[shipToColumn]?.ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }
        }

        return null;
    }

    private System.Data.DataTable? LoadQuotationData()
    {
        var quotationDir = _config.GetQuotationPath();
        if (!Directory.Exists(quotationDir))
        {
            _logger.LogWarning("报价表目录不存在: {Dir}", quotationDir);
            return null;
        }

        // 查找报价表文件（包含"报价表"但不包含"upsell"）
        var quotationFiles = Directory.GetFiles(quotationDir, "*报价表.csv")
            .Where(f => !f.Contains("upsell", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (quotationFiles.Count == 0)
        {
            _logger.LogWarning("未找到报价表文件（包含'报价表'但不包含'upsell'）");
            return null;
        }

        try
        {
            return LoadCsvToDataTable(quotationFiles[0]);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取报价表文件失败: {File}", quotationFiles[0]);
            return null;
        }
    }

    private System.Data.DataTable? LoadUpsellQuotationData()
    {
        var quotationDir = _config.GetQuotationPath();
        if (!Directory.Exists(quotationDir))
        {
            return null;
        }

        // 查找 upsell 报价表文件
        var upsellFiles = Directory.GetFiles(quotationDir, "*upsell*报价表.csv")
            .Concat(Directory.GetFiles(quotationDir, "*报价表*upsell*.csv"))
            .ToList();

        if (upsellFiles.Count == 0)
        {
            _logger.LogWarning("未找到upsell报价表文件（包含'upsell'和'报价表'）");
            return null;
        }

        try
        {
            return LoadCsvToDataTable(upsellFiles[0]);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取upsell报价表文件失败: {File}", upsellFiles[0]);
            return null;
        }
    }

    private System.Data.DataTable LoadCsvToDataTable(string csvFile)
    {
        var dataTable = new System.Data.DataTable();
        var lines = File.ReadAllLines(csvFile, Encoding.UTF8);

        if (lines.Length == 0) return dataTable;

        // 读取表头
        var headers = ParseCsvLine(lines[0]);
        foreach (var header in headers)
        {
            dataTable.Columns.Add(header, typeof(string));
        }

        // 读取数据
        for (int i = 1; i < lines.Length; i++)
        {
            var values = ParseCsvLine(lines[i]);
            var row = dataTable.NewRow();
            for (int j = 0; j < Math.Min(values.Length, dataTable.Columns.Count); j++)
            {
                row[j] = values[j];
            }
            dataTable.Rows.Add(row);
        }

        return dataTable;
    }

    private string[] ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        foreach (var ch in line)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (ch == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        values.Add(current.ToString());
        return values.ToArray();
    }

    private void AddSumRow(System.Data.DataTable data)
    {
        if (data.Rows.Count == 0) return;

        var newRow = data.NewRow();
        data.Rows.Add(newRow);

        // 找到 cost 列的位置
        var costColIdx = data.Columns.IndexOf("cost");
        var upsellColIdx = data.Columns.IndexOf("upsell");

        if (costColIdx < 0 || upsellColIdx < 0) return;

        // 在 cost 列左边的列显示 "sum"
        if (costColIdx > 0)
        {
            newRow[costColIdx - 1] = "sum";
        }
        else
        {
            newRow[0] = "sum";
        }

        // 设置公式：SUM(cost列范围)+SUM(upsell列范围)
        var costColLetter = GetExcelColumnLetter(costColIdx + 1);
        var upsellColLetter = GetExcelColumnLetter(upsellColIdx + 1);
        var dataStartRow = 2; // Excel 行号从 1 开始，第 1 行是标题
        var dataEndRow = data.Rows.Count; // 最后一行数据（不包括 sum 行）

        var formula = $"=SUM({costColLetter}{dataStartRow}:{costColLetter}{dataEndRow})+SUM({upsellColLetter}{dataStartRow}:{upsellColLetter}{dataEndRow})";
        newRow["cost"] = formula;

        _logger.LogInformation("已添加sum行，公式: {Formula}", formula);
    }

    private string GetExcelColumnLetter(int columnNumber)
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

    private void ReorderColumn(System.Data.DataTable data, string columnToMove, string referenceColumn, bool after)
    {
        if (!data.Columns.Contains(columnToMove) || !data.Columns.Contains(referenceColumn))
            return;

        var moveCol = data.Columns[columnToMove];
        var refCol = data.Columns[referenceColumn];

        // 获取当前索引
        var moveColIdx = moveCol?.Ordinal ?? -1;
        var refColIdx = refCol?.Ordinal ?? -1;

        if (moveColIdx < 0 || refColIdx < 0) return;

        // 如果已经在正确位置，不需要移动
        if (after && moveColIdx == refColIdx + 1) return;
        if (!after && moveColIdx == refColIdx - 1) return;

        // 计算目标位置
        int targetIdx;
        if (after)
        {
            targetIdx = refColIdx + 1;
            if (moveColIdx < refColIdx)
            {
                // 如果移动的列在参考列之前，目标位置需要减1
                targetIdx--;
            }
        }
        else
        {
            targetIdx = refColIdx;
            if (moveColIdx > refColIdx)
            {
                // 如果移动的列在参考列之后，目标位置需要加1
                targetIdx++;
            }
        }

        // 移动列
        if (moveColIdx != targetIdx && moveCol != null)
        {
            moveCol.SetOrdinal(targetIdx);
        }
    }

    private void SaveAsExcel(System.Data.DataTable data, string outputFile, string sheetName)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(string.IsNullOrEmpty(sheetName) ? "Sheet1" : sheetName);

        // 写入表头
        for (int col = 0; col < data.Columns.Count; col++)
        {
            worksheet.Cell(1, col + 1).Value = data.Columns[col].ColumnName;
        }

        // 写入数据
        for (int row = 0; row < data.Rows.Count; row++)
        {
            for (int col = 0; col < data.Columns.Count; col++)
            {
                var value = data.Rows[row][col];
                var cell = worksheet.Cell(row + 2, col + 1);

                // 获取列名，用于特殊处理
                var colName = data.Columns[col].ColumnName;
                var isCostOrUpsell = colName == "cost" || colName == "upsell";

                // 如果是公式，设置公式；否则设置值
                if (value?.ToString()?.StartsWith("=") == true)
                {
                    cell.FormulaA1 = value.ToString();
                }
                else
                {
                    // ClosedXML 需要将值转换为 XLCellValue
                    if (value == null || value == DBNull.Value)
                    {
                        cell.Value = string.Empty;
                    }
                    else if (value is double || value is int || value is decimal || value is float)
                    {
                        cell.Value = Convert.ToDouble(value);
                    }
                    else
                    {
                        var valueStr = value.ToString() ?? string.Empty;

                        // 对于 cost 和 upsell 列，尝试将字符串转换为数字
                        if (isCostOrUpsell && !string.IsNullOrWhiteSpace(valueStr))
                        {
                            // 检查是否是错误信息
                            if (valueStr.Contains("未找到") || valueStr.Contains("不存在"))
                            {
                                // 保持为字符串，稍后会设置红色背景
                                cell.Value = valueStr;
                            }
                            else if (double.TryParse(valueStr, out var numValue))
                            {
                                // 转换为数字，以便应用货币格式
                                cell.Value = numValue;
                            }
                            else
                            {
                                // 无法转换为数字，保持为字符串
                                cell.Value = valueStr;
                            }
                        }
                        else
                        {
                            cell.Value = valueStr;
                        }
                    }
                }

                // 设置 cost 和 upsell 列的货币格式
                if (isCostOrUpsell)
                {
                    // 设置货币格式：US$ 符号，2位小数
                    cell.Style.NumberFormat.Format = "$#,##0.00";

                    // 如果是错误信息，设置红色背景
                    var valueStr = value?.ToString() ?? "";
                    if (valueStr.Contains("未找到") || valueStr.Contains("不存在"))
                    {
                        cell.Style.Fill.PatternType = XLFillPatternValues.Solid;
                        cell.Style.Fill.BackgroundColor = XLColor.Red;
                        cell.Style.Font.FontColor = XLColor.White;
                    }
                }
                else if (value?.ToString()?.Contains("未找到") == true ||
                         value?.ToString()?.Contains("不存在") == true)
                {
                    // 其他列的错误信息也设置为红色
                    cell.Style.Fill.PatternType = XLFillPatternValues.Solid;
                    cell.Style.Fill.BackgroundColor = XLColor.Red;
                    cell.Style.Font.FontColor = XLColor.White;
                }
            }
        }

        // 冻结表头（冻结前2行，前1列）
        worksheet.SheetView.FreezeRows(2);
        worksheet.SheetView.FreezeColumns(1);

        // 高亮显示最后一行（sum 行）
        if (data.Rows.Count > 0)
        {
            var lastRow = data.Rows.Count + 1;
            var sumColIdx = -1;

            // 找到显示 "sum" 的列
            for (int col = 0; col < data.Columns.Count; col++)
            {
                if (worksheet.Cell(lastRow, col + 1).Value.ToString() == "sum")
                {
                    sumColIdx = col + 1;
                    break;
                }
            }

            // 高亮显示
            var highlightFill = XLColor.Yellow;

            if (sumColIdx > 0)
            {
                var sumCell = worksheet.Cell(lastRow, sumColIdx);
                sumCell.Style.Fill.PatternType = XLFillPatternValues.Solid;
                sumCell.Style.Fill.BackgroundColor = highlightFill;
                sumCell.Style.Font.Bold = true;
                sumCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            var costColIdx = data.Columns.IndexOf("cost") + 1;
            if (costColIdx > 0)
            {
                var costCell = worksheet.Cell(lastRow, costColIdx);
                costCell.Style.Fill.PatternType = XLFillPatternValues.Solid;
                costCell.Style.Fill.BackgroundColor = highlightFill;
                costCell.Style.Font.Bold = true;
            }
        }

        // 自动调整列宽
        worksheet.Columns().AdjustToContents();

        workbook.SaveAs(outputFile);
    }

    private void DeleteSourceFile(string filePath, string relativePath)
    {
        // 如果文件不存在，直接返回（可能已被删除）
        if (!File.Exists(filePath))
        {
            _logger.LogDebug("文件不存在，跳过删除: {RelativePath}", relativePath);
            return;
        }

        const int maxRetries = 5;
        const int retryDelayMs = 500;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                if (attempt > 0)
                {
                    Thread.Sleep(retryDelayMs);
                }

                File.Delete(filePath);
                _logger.LogInformation("✓ 已删除原文件: {RelativePath}", relativePath);
                return;
            }
            catch (UnauthorizedAccessException)
            {
                if (attempt < maxRetries - 1)
                {
                    _logger.LogWarning("删除原文件失败（文件可能被占用），{Delay}ms后重试 ({Attempt}/{MaxRetries}): {RelativePath}",
                        retryDelayMs, attempt + 1, maxRetries, relativePath);
                }
                else
                {
                    _logger.LogWarning("删除原文件失败（文件被占用，已重试{MaxRetries}次，请手动关闭文件后删除）: {RelativePath}",
                        maxRetries, relativePath);
                    // 不记录为错误，只是警告，因为文件可能被其他程序打开
                }
            }
            catch (FileNotFoundException)
            {
                // 文件已被删除，正常情况
                _logger.LogDebug("文件已被删除: {RelativePath}", relativePath);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除原文件失败: {RelativePath}", relativePath);
                return;
            }
        }
    }

    /// <summary>
    /// 使用 OpenXML 加载工作簿，跳过图片部分
    /// </summary>
    private XLWorkbook LoadWorkbookWithoutPictures(string filePath)
    {
        var newWorkbook = new XLWorkbook();

        using (var spreadsheetDocument = SpreadsheetDocument.Open(filePath, false))
        {
            var workbookPart = spreadsheetDocument.WorkbookPart;
            if (workbookPart == null)
            {
                throw new InvalidOperationException("无法读取工作簿");
            }

            var sheets = workbookPart.Workbook?.GetFirstChild<Sheets>();
            if (sheets == null)
            {
                throw new InvalidOperationException("无法读取工作表");
            }

            var sharedStringTable = workbookPart.SharedStringTablePart?.SharedStringTable;

            foreach (var sheet in sheets.Elements<Sheet>())
            {
                var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
                var worksheet = worksheetPart.Worksheet;
                var sheetData = worksheet?.GetFirstChild<SheetData>();

                if (sheetData == null) continue;

                var newWorksheet = newWorkbook.Worksheets.Add(sheet.Name ?? "Sheet1");

                // 读取数据
                int rowIndex = 1;
                foreach (var row in sheetData.Elements<Row>())
                {
                    int colIndex = 1;
                    foreach (var cell in row.Elements<Cell>())
                    {
                        var cellValue = GetCellValue(cell, sharedStringTable);
                        if (!string.IsNullOrEmpty(cellValue))
                        {
                            var xlCell = newWorksheet.Cell(rowIndex, colIndex);

                            // 尝试解析为数字
                            if (double.TryParse(cellValue, out var numValue))
                            {
                                xlCell.Value = numValue;
                            }
                            else if (cellValue.StartsWith("="))
                            {
                                xlCell.FormulaA1 = cellValue;
                            }
                            else
                            {
                                xlCell.Value = cellValue;
                            }
                        }
                        colIndex++;
                    }
                    rowIndex++;
                }
            }
        }

        return newWorkbook;
    }

    /// <summary>
    /// 获取单元格值
    /// </summary>
    private string GetCellValue(Cell cell, SharedStringTable? sharedStringTable)
    {
        if (cell.CellValue == null)
            return string.Empty;

        var cellValue = cell.CellValue.Text;

        // 如果是共享字符串，从共享字符串表中获取
        if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
        {
            if (sharedStringTable != null && int.TryParse(cellValue, out var index))
            {
                var item = sharedStringTable.Elements<SharedStringItem>().ElementAt(index);
                if (item != null)
                {
                    return item.Text?.Text ?? string.Empty;
                }
            }
        }

        return cellValue ?? string.Empty;
    }

    private void GeneratePurchaseTable(IXLWorksheet ordersWorksheet, string outputFile, string baseName)
    {
        _logger.LogInformation("开始处理采购表生成 - 输出文件: {OutputFile}", outputFile);

        // 读取 Orders 数据
        var ordersData = ReadWorksheetToDataTable(ordersWorksheet);
        _logger.LogInformation("读取Orders数据完成 - 行数: {RowCount}, 列数: {ColumnCount}",
            ordersData.Rows.Count, ordersData.Columns.Count);

        if (ordersData.Rows.Count == 0)
        {
            _logger.LogWarning("Orders 数据为空，跳过生成采购表");
            return;
        }

        // 修复缺失的商品SKU值（采购表生成也需要完整的商品SKU数据）
        FixMissingProductSku(ordersData);

        // 查找商品SKU列、QTY列和Product name列
        string? productSkuColumn = null;
        string? qtyColumn = null;
        string? productNameColumn = null;

        foreach (System.Data.DataColumn col in ordersData.Columns)
        {
            var colName = col.ColumnName.Trim();
            if (colName.Contains("商品SKU", StringComparison.OrdinalIgnoreCase))
            {
                productSkuColumn = col.ColumnName;
            }
            if (colName.Contains("QTY", StringComparison.OrdinalIgnoreCase) &&
                !colName.Contains("MERGED", StringComparison.OrdinalIgnoreCase))
            {
                qtyColumn = col.ColumnName;
            }
            if (colName.Contains("Product name", StringComparison.OrdinalIgnoreCase) ||
                colName.Contains("产品名称", StringComparison.OrdinalIgnoreCase))
            {
                productNameColumn = col.ColumnName;
            }
        }

        if (productSkuColumn == null || qtyColumn == null)
        {
            _logger.LogWarning("未找到商品SKU列或QTY列，跳过生成采购表 - 商品SKU列: {ProductSkuColumn}, QTY列: {QtyColumn}",
                productSkuColumn ?? "未找到", qtyColumn ?? "未找到");
            _logger.LogInformation("可用列名: {Columns}", string.Join(", ", ordersData.Columns.Cast<System.Data.DataColumn>().Select(c => c.ColumnName)));
            return;
        }

        _logger.LogInformation("找到列 - 商品SKU列: {ProductSkuColumn}, QTY列: {QtyColumn}, Product name列: {ProductNameColumn}",
            productSkuColumn, qtyColumn, productNameColumn ?? "未找到");

        // 按商品SKU合并数量，同时保存Product name信息
        var skuGroups = ordersData.Rows.Cast<System.Data.DataRow>()
            .GroupBy(row => row[productSkuColumn]?.ToString()?.Trim() ?? "")
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .Select(g => new
            {
                Sku = g.Key,
                TotalQty = g.Sum(row =>
                {
                    var qtyStr = row[qtyColumn]?.ToString()?.Trim() ?? "";
                    if (int.TryParse(qtyStr, out var qty))
                        return qty;
                    return 0;
                }),
                // 获取第一个非空的Product name（同一SKU的Product name应该相同）
                ProductName = productNameColumn != null
                    ? g.Select(row => row[productNameColumn]?.ToString()?.Trim() ?? "")
                      .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? ""
                    : ""
            })
            .Where(x => x.TotalQty > 0)
            .OrderBy(x => x.Sku)
            .ToList();

        _logger.LogInformation("按SKU分组完成 - 分组数: {GroupCount}", skuGroups.Count);

        if (skuGroups.Count == 0)
        {
            _logger.LogWarning("没有有效的SKU数据，跳过生成采购表");
            return;
        }

        // 加载找货表
        var quotationData = LoadQuotationDataForPurchase();
        if (quotationData == null)
        {
            _logger.LogWarning("未找到找货表，使用空数据生成采购表");
            quotationData = new System.Data.DataTable();
        }
        else
        {
            _logger.LogInformation("加载找货表完成 - 行数: {RowCount}, 列数: {ColumnCount}",
                quotationData.Rows.Count, quotationData.Columns.Count);
        }

        // 创建采购表数据
        var purchaseData = new System.Data.DataTable();
        purchaseData.Columns.Add("SKU", typeof(string));
        purchaseData.Columns.Add("SKU_商品名", typeof(string));
        purchaseData.Columns.Add("size", typeof(string));
        purchaseData.Columns.Add("图片", typeof(string));
        purchaseData.Columns.Add("应采", typeof(int));
        purchaseData.Columns.Add("快递单号", typeof(string));
        purchaseData.Columns.Add("备注", typeof(string));
        purchaseData.Columns.Add("采购链接", typeof(string));

        // 查找找货表中的列
        string? quotationSkuColumn = null;
        string? quotationNameColumn = null;
        string? quotationSizeColumn = null;
        string? quotationImageColumn = null;
        string? quotationLinkColumn = null;

        // 记录找货表的所有列名，便于调试
        var allColumns = quotationData.Columns.Cast<System.Data.DataColumn>()
            .Select(c => c.ColumnName)
            .ToList();
        _logger.LogInformation("找货表所有列名: {Columns}", string.Join(", ", allColumns));

        foreach (System.Data.DataColumn col in quotationData.Columns)
        {
            var colName = col.ColumnName.Trim();
            var colNameUpper = colName.ToUpper();

            if (colNameUpper.Contains("SKU") && quotationSkuColumn == null)
            {
                quotationSkuColumn = col.ColumnName;
                _logger.LogInformation("找到SKU列: {ColumnName}", col.ColumnName);
            }

            // 优先查找"产品中文名字"列
            if (quotationNameColumn == null)
            {
                if (colName.Contains("产品中文名字", StringComparison.OrdinalIgnoreCase) ||
                    colName.Contains("产品中文名称", StringComparison.OrdinalIgnoreCase) ||
                    colName.Equals("产品中文名字", StringComparison.OrdinalIgnoreCase))
                {
                    quotationNameColumn = col.ColumnName;
                    _logger.LogInformation("找到产品中文名字列（精确匹配）: {ColumnName}", col.ColumnName);
                }
                else if ((colName.Contains("商品", StringComparison.OrdinalIgnoreCase) &&
                         (colName.Contains("名字", StringComparison.OrdinalIgnoreCase) ||
                          colName.Contains("名称", StringComparison.OrdinalIgnoreCase))))
                {
                    quotationNameColumn = col.ColumnName;
                    _logger.LogInformation("找到产品中文名字列（包含商品和名字/名称）: {ColumnName}", col.ColumnName);
                }
                else if (colName.Contains("商品", StringComparison.OrdinalIgnoreCase) ||
                         (colName.Contains("名称", StringComparison.OrdinalIgnoreCase) &&
                          !colName.Contains("SKU", StringComparison.OrdinalIgnoreCase)))
                {
                    quotationNameColumn = col.ColumnName;
                    _logger.LogInformation("找到产品中文名字列（备选）: {ColumnName}", col.ColumnName);
                }
            }

            if (colNameUpper.Contains("SIZE") && quotationSizeColumn == null)
            {
                quotationSizeColumn = col.ColumnName;
            }
            if ((colNameUpper.Contains("图片") || colNameUpper.Contains("IMAGE") || colNameUpper.Contains("PIC")) &&
                quotationImageColumn == null)
            {
                quotationImageColumn = col.ColumnName;
            }
            if ((colNameUpper.Contains("链接") || colNameUpper.Contains("LINK") || colNameUpper.Contains("URL")) &&
                quotationLinkColumn == null)
            {
                quotationLinkColumn = col.ColumnName;
            }
        }

        _logger.LogInformation("找货表列匹配结果 - SKU列: {SkuColumn}, 产品中文名字列: {NameColumn}, Size列: {SizeColumn}, 图片列: {ImageColumn}, 链接列: {LinkColumn}",
            quotationSkuColumn ?? "未找到",
            quotationNameColumn ?? "未找到",
            quotationSizeColumn ?? "未找到",
            quotationImageColumn ?? "未找到",
            quotationLinkColumn ?? "未找到");

        // 生成采购表数据行
        foreach (var skuGroup in skuGroups)
        {
            var row = purchaseData.NewRow();
            row["SKU"] = skuGroup.Sku;
            row["应采"] = skuGroup.TotalQty;

            // 从SKU中提取size（如果找货表中没有）
            var skuParts = skuGroup.Sku.Split('-');
            if (skuParts.Length > 0)
            {
                var lastPart = skuParts[skuParts.Length - 1].Trim();
                // 如果最后一部分看起来像size（如 S, M, L, XL, 2XL, 3XL, 均码等）
                if (lastPart.Length <= 5 && (lastPart.All(char.IsLetterOrDigit) || lastPart == "均码"))
                {
                    row["size"] = lastPart;
                }
            }

            // 从找货表中查找匹配的信息
            // 使用商品SKU的前缀（前两个部分，如 YOG-001）去匹配找货表中的SKU列
            string? productChineseName = null;
            if (quotationSkuColumn != null)
            {
                // 提取SKU前缀（前两个部分）
                var skuPrefix = ExtractSkuPrefix(skuGroup.Sku);
                _logger.LogInformation("查找找货表 - SKU: {Sku}, SKU前缀: {SkuPrefix}", skuGroup.Sku, skuPrefix);

                if (!string.IsNullOrWhiteSpace(skuPrefix))
                {
                    var matchingRows = quotationData.Rows.Cast<System.Data.DataRow>()
                        .Where(r =>
                        {
                            var quotationSku = r[quotationSkuColumn]?.ToString()?.Trim() ?? "";
                            // 提取找货表中SKU的前缀
                            var quotationSkuPrefix = ExtractSkuPrefix(quotationSku);
                            // 匹配前缀（不区分大小写）
                            return quotationSkuPrefix.Equals(skuPrefix, StringComparison.OrdinalIgnoreCase) ||
                                   quotationSku.Equals(skuPrefix, StringComparison.OrdinalIgnoreCase) ||
                                   quotationSku.Equals(skuGroup.Sku, StringComparison.OrdinalIgnoreCase);
                        })
                        .ToList();

                    _logger.LogInformation("找到匹配行数: {Count}", matchingRows.Count);

                    if (matchingRows.Count > 0)
                    {
                        var matchRow = matchingRows[0];
                        if (quotationNameColumn != null)
                        {
                            productChineseName = matchRow[quotationNameColumn]?.ToString()?.Trim() ?? "";
                            _logger.LogInformation("找到产品中文名字: {ProductChineseName}", productChineseName);
                        }
                        if (quotationSizeColumn != null && !string.IsNullOrWhiteSpace(matchRow[quotationSizeColumn]?.ToString()))
                            row["size"] = matchRow[quotationSizeColumn]?.ToString() ?? "";
                        if (quotationImageColumn != null)
                            row["图片"] = matchRow[quotationImageColumn]?.ToString() ?? "";
                        if (quotationLinkColumn != null)
                            row["采购链接"] = matchRow[quotationLinkColumn]?.ToString() ?? "";
                    }
                    else
                    {
                        _logger.LogWarning("未在找货表中找到匹配的SKU: {SkuPrefix}", skuPrefix);
                    }
                }
            }

            // 生成B列（SKU_商品名）的值
            // 1. 从找货表获取产品中文名字
            // 2. 从Orders文件获取Product name（去除尺寸字符）
            // 3. 组合：产品中文名字-Product name（去除尺寸后）
            // 4. 如果两者重复，只取产品中文名字
            var productNameWithoutSize = RemoveSizeFromProductName(skuGroup.ProductName);
            var finalProductName = BuildProductNameForPurchase(productChineseName, productNameWithoutSize);
            row["SKU_商品名"] = finalProductName;

            _logger.LogInformation("生成B列值 - SKU: {Sku}, 产品中文名字: {ProductChineseName}, Product name(去除尺寸): {ProductNameWithoutSize}, 最终值: {FinalProductName}",
                skuGroup.Sku, productChineseName ?? "未找到", productNameWithoutSize, finalProductName);

            purchaseData.Rows.Add(row);
        }

        // 添加SUM行
        var sumRow = purchaseData.NewRow();
        sumRow["图片"] = "SUM";
        var totalQty = skuGroups.Sum(g => g.TotalQty);
        sumRow["应采"] = totalQty;
        purchaseData.Rows.Add(sumRow);

        _logger.LogInformation("采购表数据准备完成 - 数据行数: {RowCount}", purchaseData.Rows.Count);

        // 保存为Excel
        SavePurchaseTableAsExcel(purchaseData, outputFile, baseName);

        _logger.LogInformation("采购表生成完成 - 文件: {OutputFile}", outputFile);
    }

    /// <summary>
    /// 提取Item name的主要部分，去掉颜色和尺寸信息
    /// 例如："Legging 3D Ceralia® – Redéfinissez votre silhouette sans effort - Bleu / S" 
    /// 提取为："Legging 3D Ceralia® – Redéfinissez votre silhouette sans effort"
    /// </summary>
    private string ExtractItemNameMainPart(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            return "";

        var trimmed = itemName.Trim();

        // 查找最后一个 "-" 的位置（用于分隔产品名称和颜色/尺寸信息）
        // 格式通常是："产品名称 - 颜色 / 尺寸" 或 "产品名称 - 颜色/尺寸"
        var lastDashIndex = trimmed.LastIndexOf(" - ");
        if (lastDashIndex > 0)
        {
            // 如果找到 " - "，提取之前的部分
            var mainPart = trimmed.Substring(0, lastDashIndex).Trim();
            // 验证提取的部分是否合理（至少包含一些字符）
            if (mainPart.Length > 5) // 至少5个字符，避免误判
            {
                return mainPart;
            }
        }

        // 如果没有找到 " - "，尝试查找单个 "-"（前后有空格）
        var singleDashIndex = trimmed.LastIndexOf('-');
        if (singleDashIndex > 0 && singleDashIndex < trimmed.Length - 1)
        {
            // 检查 "-" 前后是否有空格
            var beforeDash = trimmed.Substring(0, singleDashIndex).TrimEnd();
            var afterDash = trimmed.Substring(singleDashIndex + 1).TrimStart();

            // 如果 "-" 后面的部分看起来像颜色和尺寸（包含 "/" 或常见的尺寸字符）
            if (afterDash.Contains("/") ||
                Regex.IsMatch(afterDash, @"^[A-Za-z]+\s*/\s*[A-Za-z0-9]+", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(afterDash, @"^[A-Za-z]+\s*/\s*[SMXL0-9]+", RegexOptions.IgnoreCase))
            {
                return beforeDash;
            }
        }

        // 如果无法识别格式，返回原始值
        return trimmed;
    }

    private string RemoveSizeFromProductName(string productName)
    {
        if (string.IsNullOrWhiteSpace(productName))
            return "";

        // 常见的尺寸模式：S, M, L, XL, XXL, 2XL, 3XL, 4XL, 5XL, 均码等
        // 去除尺寸字符，包括前后的空格和连字符
        var sizePatterns = new[]
        {
            @"\s*-\s*[0-9]*XL\s*$",  // -XL, -2XL, -3XL等
            @"\s*-\s*[SM]\s*$",      // -S, -M
            @"\s*-\s*L\s*$",          // -L
            @"\s*-\s*均码\s*$",       // -均码
            @"\s*[0-9]*XL\s*$",       // XL, 2XL, 3XL等（无连字符）
            @"\s*[SM]\s*$",           // S, M（无连字符）
            @"\s*L\s*$",              // L（无连字符）
            @"\s*均码\s*$"            // 均码（无连字符）
        };

        var result = productName.Trim();
        foreach (var pattern in sizePatterns)
        {
            result = Regex.Replace(result, pattern, "", RegexOptions.IgnoreCase);
        }

        return result.Trim();
    }

    private string BuildProductNameForPurchase(string? productChineseName, string productNameWithoutSize)
    {
        // 如果产品中文名字为空，使用Product name（去除尺寸后）
        if (string.IsNullOrWhiteSpace(productChineseName))
        {
            return productNameWithoutSize;
        }

        // 如果Product name（去除尺寸后）为空，只使用产品中文名字
        if (string.IsNullOrWhiteSpace(productNameWithoutSize))
        {
            return productChineseName;
        }

        // 如果两者相同或重复，只取产品中文名字
        if (productChineseName.Equals(productNameWithoutSize, StringComparison.OrdinalIgnoreCase) ||
            productChineseName.Contains(productNameWithoutSize, StringComparison.OrdinalIgnoreCase) ||
            productNameWithoutSize.Contains(productChineseName, StringComparison.OrdinalIgnoreCase))
        {
            return productChineseName;
        }

        // 组合：产品中文名字-Product name（去除尺寸后）
        return $"{productChineseName}-{productNameWithoutSize}";
    }

    private System.Data.DataTable? LoadQuotationDataForPurchase()
    {
        var quotationDir = _config.GetQuotationPath();
        if (!Directory.Exists(quotationDir))
        {
            _logger.LogWarning("报价表目录不存在: {Dir}", quotationDir);
            return null;
        }

        // 查找找货表文件（包含"找货"或"找货表"）
        var quotationFiles = Directory.GetFiles(quotationDir, "*.csv")
            .Where(f =>
            {
                var fileName = Path.GetFileName(f);
                return fileName.Contains("找货", StringComparison.OrdinalIgnoreCase) ||
                       fileName.Contains("找货表", StringComparison.OrdinalIgnoreCase);
            })
            .OrderByDescending(f => File.GetLastWriteTime(f)) // 优先使用最新的文件
            .ToList();

        if (quotationFiles.Count == 0)
        {
            _logger.LogWarning("未找到找货表文件（包含'找货'或'找货表'）");
            _logger.LogInformation("报价表目录中的CSV文件: {Files}",
                string.Join(", ", Directory.GetFiles(quotationDir, "*.csv").Select(Path.GetFileName)));
            return null;
        }

        _logger.LogInformation("找到找货表文件: {File}", Path.GetFileName(quotationFiles[0]));

        try
        {
            return LoadCsvToDataTable(quotationFiles[0]);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取找货表文件失败: {File}", quotationFiles[0]);
            return null;
        }
    }

    private void SavePurchaseTableAsExcel(System.Data.DataTable data, string outputFile, string baseName)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("采购");

        // 写入标题行（第1行）
        worksheet.Cell(1, 1).Value = $"LG-Le-7天库存采购-{baseName}";
        worksheet.Range(1, 1, 1, 8).Merge();

        // 写入业务信息行（第2行）
        worksheet.Cell(2, 1).Value = "业务:伍菲-助理:嘉卉-采购:陈诺 仓库:东梅";
        worksheet.Range(2, 1, 2, 8).Merge();

        // 写入表头（第3行）
        var headers = new[] { "SKU", "SKU", "size", "图片", "应采", "快递单号", "备注", "采购链接" };
        for (int col = 0; col < headers.Length; col++)
        {
            worksheet.Cell(3, col + 1).Value = headers[col];
        }

        // 写入数据（从第4行开始）
        for (int row = 0; row < data.Rows.Count; row++)
        {
            var dataRow = data.Rows[row];
            var excelRow = row + 4;

            worksheet.Cell(excelRow, 1).Value = dataRow["SKU"]?.ToString() ?? "";
            worksheet.Cell(excelRow, 2).Value = dataRow["SKU_商品名"]?.ToString() ?? "";
            worksheet.Cell(excelRow, 3).Value = dataRow["size"]?.ToString() ?? "";

            // 图片列（可能是公式）
            var imageValue = dataRow["图片"]?.ToString() ?? "";
            if (imageValue.StartsWith("="))
            {
                worksheet.Cell(excelRow, 4).FormulaA1 = imageValue;
            }
            else
            {
                worksheet.Cell(excelRow, 4).Value = imageValue;
            }

            // 应采列（数字）
            if (dataRow["应采"] != DBNull.Value && dataRow["应采"] != null)
            {
                if (int.TryParse(dataRow["应采"].ToString(), out var qty))
                {
                    worksheet.Cell(excelRow, 5).Value = qty;
                }
            }

            worksheet.Cell(excelRow, 6).Value = dataRow["快递单号"]?.ToString() ?? "";
            worksheet.Cell(excelRow, 7).Value = dataRow["备注"]?.ToString() ?? "";
            worksheet.Cell(excelRow, 8).Value = dataRow["采购链接"]?.ToString() ?? "";

            // 如果是SUM行，设置公式
            if (imageValue == "SUM")
            {
                var qtyColLetter = GetExcelColumnLetter(5);
                var dataStartRow = 4;
                var dataEndRow = excelRow - 1;
                worksheet.Cell(excelRow, 5).FormulaA1 = $"=SUM({qtyColLetter}{dataStartRow}:{qtyColLetter}{dataEndRow})";

                // 高亮SUM行
                worksheet.Range(excelRow, 1, excelRow, 8).Style.Fill.PatternType = XLFillPatternValues.Solid;
                worksheet.Range(excelRow, 1, excelRow, 8).Style.Fill.BackgroundColor = XLColor.Yellow;
                worksheet.Range(excelRow, 1, excelRow, 8).Style.Font.Bold = true;
            }
        }

        // 冻结前3行
        worksheet.SheetView.FreezeRows(3);

        // 自动调整列宽
        worksheet.Columns().AdjustToContents();

        workbook.SaveAs(outputFile);

        var fileInfo = new FileInfo(outputFile);
        if (fileInfo.Exists)
        {
            _logger.LogInformation("采购表Excel文件保存完成 - 文件: {OutputFile}, 文件大小: {FileSize} bytes",
                outputFile, fileInfo.Length);
        }
        else
        {
            _logger.LogError("采购表Excel文件保存失败 - 文件不存在: {OutputFile}", outputFile);
        }
    }
}
