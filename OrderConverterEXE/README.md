# OrderConverterEXE（xlsx-converter）- .NET 8 后台转换服务

自动递归扫描 `docs/sourceData/` 及其子目录，把 Excel（`.xlsx`）转换为 CSV 或 Excel。若文件位于 `**/Orders/**` 路径下，会触发订单专用处理（计算列 + 报价表匹配 + 生成汇总行），并在 `LG-Le/**/Orders/` 额外生成采购表。

## 功能

### 基础能力
- **递归扫描**：每隔 `10` 秒扫描一次（可在 `Configuration.cs` 修改）
- **保持目录结构**：输出目录与输入目录保持相同层级结构
- **转换后删除源文件**：本轮扫描全部处理完成后，再统一删除源 `.xlsx`（带重试）
- **覆盖输出**：若目标文件已存在会覆盖
- **跳过内部/空 Sheet**：`RangeUsed == null` 或 sheet 名包含 `WpsReserved` / `Reserved` 会跳过
- **中文支持**：控制台输出 UTF-8；CSV 以 UTF-8 BOM 写出

### 非 Orders 目录（默认）
- 每个工作表输出一个 CSV 文件：`{安全文件名}_{安全sheet名}.csv`
- 若 sheet 名为空：`{安全文件名}_sheet1.csv`

### Orders 目录（订单专用）
触发条件：相对路径中包含 `Orders`（不区分大小写）。

对每个 Orders 文件，程序会输出 **两份 Excel**（同一目录下）：

- **onlyShip 版本**（使用报价表的 `Ship to XX...` 列）：  
  `onlyShip_{上上级目录} {上级目录} {日期英文} tracking & cost.xlsx`
- **Total to 版本**（使用报价表的 `Total to {Country}` 列，列名需要精确匹配）：  
  `{上上级目录} {上级目录} {日期英文} tracking & cost.xlsx`

> 说明：输出文件名与“工作表名称”无关（由目录 + 文件名日期决定）。如果同一 Orders 工作簿含多个 sheet，后处理的 sheet 会覆盖先生成的同名输出（通常订单文件仅 1 个 sheet）。

#### Orders 数据处理（与代码一致）
每次处理会按顺序执行：

1. **修复缺失的 `商品SKU`**
   - 依赖 `Item name` 列（大小写不敏感匹配 `Item` + `name`）
   - 根据“Item name 主体部分”（去掉颜色/尺寸信息）建立映射：`Item name主体 -> 商品SKU`
   - 对 `商品SKU` 为空的行进行回填

2. **删除不需要的列**
   - 删除列名（不区分大小写）：`Item cost`、`Shipping cost`

3. **添加 `SKU_Quotation`**
   - 优先使用 `商品SKU` 列；若找不到则退化为任意包含 `SKU` 且不含 `QUOTATION` 的列
   - 若同时存在 `SKU` 列且与 `商品SKU` 不同，会用 `商品SKU` 覆盖 `SKU`
   - `SKU_Quotation` 为 SKU 前缀（按 `-` 分割取前两段）：`YOG-001-Bleu-L → YOG-001`
   - 列位置：放在 `SKU`（或 `商品SKU`）右侧

4. **添加 `QTY_Merged`**
   - 按 `Order#` + `SKU_Quotation` 分组，对 `QTY` 求和
   - 只在每组第一行显示合计，其余行为空
   - 列位置：放在 `QTY` 右侧

5. **添加 `cost` / `upsell`**
   - 列位置：插入到 `Country`（或 `国家`）左侧，并保证 `upsell` 在 `cost` 右侧
   - 按 `Order#` 分组：选择 `QTY_Merged` 最大的那一行写入 `cost`，其余有 `QTY_Merged` 的行写入 `upsell`
   - 若找不到对应报价表/匹配数据，会写入提示文本（并在输出 Excel 中红底白字高亮）

6. **添加 `sum` 行**
   - 在 `cost` 左侧的单元格写入 `sum`
   - 在 `cost` 单元格写入公式：`=SUM(cost列)+SUM(upsell列)`（求和范围为标题下一行到“sum 行的上一行”）

#### Orders 输出 Excel 格式
- `cost` / `upsell`：货币格式 `"$#,##0.00"`
- 错误提示（包含“未找到/不存在”）：红底白字
- 冻结：**前 2 行 + 第 1 列**
- sum 行高亮：`sum` 单元格与 `cost` 单元格为黄色背景 + 加粗

### 采购表（仅 LG-Le Orders）
触发条件：Orders 文件相对路径同时包含 `LG-Le` 与 `Orders`（不区分大小写）。

输出文件名：
`{上上级目录} {上级目录} {日期中文} 采购.xlsx`（日期从源文件名提取 `YYYYMMDD`，如 `2026年1月23日`）

采购表数据来源与规则（与代码一致）：
- 从 Orders 表中按 `商品SKU` 分组汇总 `QTY`
- 可选加载“找货表”CSV：从 `docs/convertedData/LG-Le/Quotation/` 下选择**最新**且文件名包含 `找货`/`找货表` 的 `.csv`
- 找货表匹配：
  - 按 SKU 前缀（同样取前两段，如 `YOG-001`）匹配
  - 匹配到后可补齐：中文名字、size、图片、采购链接（列名为启发式匹配）
- 采购表 Excel：
  - Sheet 名：`采购`
  - 前 2 行为合并标题行，第 3 行为表头
  - 冻结：前 3 行
  - 自动添加 SUM 行（黄色高亮）

## 目录结构（默认）
- **输入**：`docs/sourceData/`
- **输出**：`docs/convertedData/`
- **报价表/找货表目录**：`docs/convertedData/LG-Le/Quotation/`

> 注意：Orders 的 `cost/upsell` 与采购表都依赖 **输出目录** 下的 CSV（报价表/找货表）。程序会在同一轮扫描中优先处理 `Quotation` 目录下的 xlsx，再处理 `Orders`，以尽量保证依赖就绪。

## 安装与运行

### 前置要求
- .NET SDK **8.0+**

### 推荐运行方式（Windows）
- 直接运行 `run.bat`

### PowerShell
- 运行 `run.ps1`

### 命令行
```bash
cd OrderConverterEXE
dotnet restore
dotnet build
dotnet run
```

## 发布为单文件可执行
```bash
# Windows
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

发布产物目录：`bin/Release/net8.0/<runtime>/publish/`

## 配置
`Configuration.cs`：
- `BaseDir`：默认回到解决方案根目录（用于拼出 docs 路径）
- `SourceDir`：默认 `docs/sourceData`
- `TargetDir`：默认 `docs/convertedData`
- `CheckIntervalSeconds`：默认 `10`

日志级别：`appsettings.json`（默认 Information）

## 日志
- 控制台输出
- 文件：`xlsx_converter.log`

## 注意事项
- **源文件会被删除**：转换成功后会删除源 `.xlsx`；文件占用时会重试（最多 5 次，每次间隔 500ms）
- **重复转换判定**：同一路径文件若“最后修改时间”未变化会被跳过；更新/重新复制（时间变化）会重新转换并覆盖输出
