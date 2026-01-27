# Order Converter Service (order-converter)

**程序名称**: Order Converter Service  
**简称**: order-converter

自动监控指定目录，将 Excel (.xlsx) 文件转换为 CSV 或 Excel 格式，并对 Orders 目录下的文件进行特殊数据处理。

## 功能特点

### 基础功能
- ✅ 自动监控 `docs/sourceData` 目录及其所有子目录中的 xlsx 文件
- ✅ 每隔10秒自动检查新文件
- ✅ 递归扫描所有子文件夹
- ✅ 自动将 xlsx 文件的所有 sheet 转换为独立的 CSV/Excel 文件
- ✅ **保持文件夹结构**：转换后的文件按照原文件夹结构保存到 `docs/convertedData` 目录
- ✅ **转换后自动删除**：转换成功后自动删除原 xlsx 文件
- ✅ **支持重复转换**：如果重新放入相同的文件，会重新转换并覆盖已存在的文件
- ✅ 自动跳过 WPS Office 的内部 sheet（如 WpsReserved_CellImgList）
- ✅ 支持中文文件名和内容
- ✅ 详细的日志记录

### Orders 目录特殊处理

对于 `docs/sourceData/**/Orders/` 目录下的 xlsx 文件，程序会进行特殊处理：

1. **添加 SKU_Quotation 列**
   - 从 `商品SKU` 列（或 `SKU` 列）提取 SKU 前缀
   - 例如：`YOG-001-Bleu-L` → `YOG-001`
   - 列位置：位于 `SKU` 列右侧

2. **添加 QTY_Merged 列**
   - 按 `Order#` 和 `SKU_Quotation` 分组，累加 `QTY` 值
   - 只在每组的第一行显示总和，其他行为空
   - 列位置：位于 `QTY` 列右侧

3. **添加 cost 和 upsell 列**
   - 从 `docs/convertedData/LG-Le/Quotation/` 目录下的报价表文件查找数据
   - `cost`：同一 `Order#` 中 `QTY_Merged` 最大的行，根据 `SKU_Quotation`、`QTY_Merged` 和 `Country` 查找报价表
   - `upsell`：同一 `Order#` 中其他有 `QTY_Merged` 值的行，从 upsell 报价表查找
   - 如果找不到数据，会显示红色错误信息
   - 列位置：位于 `Country` 列左侧

4. **添加 sum 行**
   - 在表格最后一行显示 "sum" 文本和求和公式
   - 使用 Excel 公式：`=SUM(cost列范围)+SUM(upsell列范围)`
   - 高亮显示：黄色背景 + 粗体

5. **保存为 Excel 格式**
   - 输出格式：`.xlsx`（而不是 CSV）
   - 支持货币格式：`cost` 和 `upsell` 列显示为 US$ 格式，保留两位小数
   - 支持 Excel 公式：sum 行的值通过公式自动计算
   - 冻结表头：第一行（表头）被冻结，滚动时保持可见
   - 错误信息高亮：找不到数据时显示红色背景

### 其他目录处理

对于非 Orders 目录下的文件：
- 直接转换为 CSV 格式
- 保持原始数据结构

## 安装依赖

```bash
pip install -r requirements.txt
```

依赖包：
- `pandas` - 数据处理
- `openpyxl` - Excel 文件操作（用于 Orders 目录的文件）

## 使用方法

### 方式一：直接运行

1. **进入程序目录**：
   ```bash
   cd OrderConverter
   ```

2. **安装依赖**（如果还没安装）：
   ```bash
   pip install -r requirements.txt
   ```

3. **确保目录存在**（程序会自动创建）：
   - 源目录：`../docs/sourceData` - 放置需要转换的 xlsx 文件（支持子文件夹）
   - 目标目录：`../docs/convertedData` - 转换后的文件存储位置（保持原文件夹结构）
   - 报价表目录：`../docs/convertedData/LG-Le/Quotation/` - 报价表 CSV 文件（用于查找 cost 和 upsell）

4. **运行程序**：
   ```bash
   python xlsx_to_csv_converter.py
   ```

5. **停止程序**：按 `Ctrl+C`

## 文件命名规则

### 普通目录（非 Orders）
转换后的 CSV 文件命名格式：
- 单 sheet：`原文件名_sheet名.csv`
- 多 sheet：每个 sheet 生成一个独立的 CSV 文件

### Orders 目录
转换后的 Excel 文件命名格式：
- 单 sheet：`原文件名_sheet名.xlsx`
- 多 sheet：每个 sheet 生成一个独立的 Excel 文件

**文件夹结构保持**：
- 源文件：`docs/sourceData/LG-Le/Orders/文件.xlsx`
- 转换后：`docs/convertedData/LG-Le/Orders/文件_sheet名.xlsx`

## 报价表文件要求

为了正确计算 `cost` 和 `upsell` 值，需要在 `docs/convertedData/LG-Le/Quotation/` 目录下放置报价表文件：

1. **主报价表**：文件名包含 "报价表" 但不包含 "upsell"
   - 用于查找 `cost` 值
   - 需要包含 SKU 列、QTY 列和 Country 列（如 `Ship to BE (6-10 working days )`）

2. **Upsell 报价表**：文件名包含 "upsell" 和 "报价表"
   - 用于查找 `upsell` 值
   - 需要包含 SKU 列、QTY 列和 Country 列

**Country 列匹配规则**：
- 程序会查找列名中包含 Country 代码的列
- 例如：Country 为 `BE` 时，会查找包含 `BE` 的列（如 `Ship to BE (6-10 working days )`）

## 日志

程序运行日志会保存到 `xlsx_converter.log` 文件中，同时也会在控制台显示。

日志包含以下信息：
- 文件转换进度
- 报价表文件加载状态
- 数据处理结果
- 错误和警告信息

## 配置

可以在 `xlsx_to_csv_converter.py` 文件中修改以下配置：

```python
SOURCE_DIR = BASE_DIR / "docs" / "sourceData"      # 源目录（递归扫描所有子目录）
TARGET_DIR = BASE_DIR / "docs" / "convertedData"   # 目标目录（保持文件夹结构）
CHECK_INTERVAL = 10                                 # 检查间隔（秒）
```

## 注意事项

- ⚠️ **转换后原文件会被删除**：转换成功后，原 xlsx 文件会被自动删除
- ⚠️ **支持重复转换**：如果重新放入相同的文件，会重新转换并覆盖已存在的文件
- ⚠️ **Orders 目录文件需要特定列**：需要包含 `商品SKU`（或 `SKU`）、`Order#`、`QTY`、`Country` 等列
- ⚠️ **报价表文件必须存在**：如果找不到报价表文件，`cost` 和 `upsell` 会显示错误信息
- 程序会递归扫描所有子目录，保持文件夹结构
- 确保有足够的磁盘空间存储转换后的文件
- 大文件转换可能需要一些时间
- 如果转换失败，原文件不会被删除
- 如果原文件正在被其他程序打开，删除操作可能会失败（程序会重试5次）

## 故障排除

### 问题：cost 和 upsell 列显示 "报价表文件不存在"
**解决方案**：
1. 确保 `docs/convertedData/LG-Le/Quotation/` 目录存在
2. 确保报价表文件已转换并保存在该目录下
3. 检查报价表文件名是否包含 "报价表" 关键字

### 问题：删除原文件失败
**解决方案**：
1. 关闭正在打开源文件的程序（如 Excel）
2. 检查文件权限
3. 程序会自动重试5次，如果仍然失败，可以手动删除

### 问题：转换后的 Excel 文件格式不正确
**解决方案**：
1. 确保已安装 `openpyxl` 库：`pip install openpyxl`
2. 检查日志文件查看详细错误信息

## 更新日志

### 当前版本
- ✅ 添加 Orders 目录特殊处理（SKU_Quotation、QTY_Merged、cost、upsell）
- ✅ 支持 Excel 格式输出（Orders 目录）
- ✅ 支持货币格式和 Excel 公式
- ✅ 冻结表头功能
- ✅ 高亮显示 sum 行
- ✅ 详细的报价表文件查找日志
- ✅ 检查间隔设置为 10 秒
- ✅ 修复中文编码问题
