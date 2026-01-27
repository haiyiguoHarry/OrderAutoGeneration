# Excel 转 Markdown 需求文件转换工具

## 📋 简介

`convert_requirements_to_md.py` 是一个用于将 Excel 需求文件转换为 Markdown 格式的工具脚本。转换后的 Markdown 文件可以被 Cursor 等 AI 编辑器直接识别和读取，便于 AI 助手理解和处理需求信息。

## ✨ 功能特点

- ✅ **自动识别表头**：智能检测 Excel 文件中的表头行位置
- ✅ **多 Sheet 支持**：自动处理 Excel 文件中的所有工作表
- ✅ **标题信息提取**：自动提取并单独显示标题信息
- ✅ **数据完整性**：保留所有原始数据，包括空值和特殊格式
- ✅ **Markdown 表格**：生成标准 Markdown 表格格式，便于阅读和编辑
- ✅ **统计信息**：自动生成数据行数和列数统计
- ✅ **中文支持**：完美支持中文文件名和内容

## 🔧 环境要求

- Python 3.6 或更高版本
- pandas >= 2.0.0
- openpyxl >= 3.1.0

## 📦 安装依赖

在项目根目录下运行：

```bash
pip install pandas>=2.0.0 openpyxl>=3.1.0
```

或者使用项目已有的 requirements.txt：

```bash
pip install -r OrderConverter/requirements.txt
```

## 🚀 使用方法

### 基本用法

#### 方法 1：使用默认文件（脚本中已配置）

直接运行脚本，将转换默认的 Excel 文件：

```bash
python convert_requirements_to_md.py
```

默认文件路径：`docs/requirements/LG-Le 1.16瑜伽裤 7天库存采购.xlsx`

#### 方法 2：指定输入文件

指定要转换的 Excel 文件路径：

```bash
python convert_requirements_to_md.py "docs/requirements/你的文件.xlsx"
```

#### 方法 3：指定输入和输出文件

同时指定输入 Excel 文件和输出 Markdown 文件路径：

```bash
python convert_requirements_to_md.py "docs/requirements/输入文件.xlsx" "docs/requirements/输出文件.md"
```

### 参数说明

| 参数位置 | 参数名称 | 是否必需 | 说明 |
|---------|---------|---------|------|
| 第1个参数 | `excel_path` | 可选 | Excel 文件路径。如果不提供，使用脚本中的默认路径 |
| 第2个参数 | `output_path` | 可选 | 输出 Markdown 文件路径。如果不提供，自动在 Excel 文件同目录下生成同名 `.md` 文件 |

## 📝 使用示例

### 示例 1：转换默认文件

```bash
python convert_requirements_to_md.py
```

**输出**：
```
正在处理 Sheet 1/2: 采购
正在处理 Sheet 2/2: 计算

转换完成！
输出文件: e:\workspace\OrderAutoGeneration\docs\requirements\LG-Le 1.16瑜伽裤 7天库存采购.md
```

### 示例 2：转换指定文件

```bash
python convert_requirements_to_md.py "docs/requirements/LG-le docteur 2026.1.21号  瑜伽裤报价新.xlsx"
```

**输出**：
```
正在处理 Sheet 1/1: Sheet1

转换完成！
输出文件: e:\workspace\OrderAutoGeneration\docs\requirements\LG-le docteur 2026.1.21号  瑜伽裤报价新.md
```

### 示例 3：指定输出路径

```bash
python convert_requirements_to_md.py "docs/requirements/需求文件.xlsx" "docs/convertedData/需求文档.md"
```

## 📄 输出格式说明

转换后的 Markdown 文件结构如下：

```markdown
# 文件名

**源文件**: `原始Excel文件名.xlsx`
**Sheet数量**: 2

---

## Sheet 1: 采购

**标题信息**: LG-Le-7天库存采购-1.16 | 业务:伍菲-助理:嘉卉-采购:陈诺 仓库:东梅

### 数据表格

| SKU | SKU | size | 图片 | 应采 | 快递单号 | 备注 | 采购链接 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| YOG-001-Noir-S | 长款瑜伽裤-黑色 | S | ... | 407 |   |   | ... |
| ... | ... | ... | ... | ... | ... | ... | ... |

**数据行数**: 63
**列数**: 8

---

## Sheet 2: 计算

...
```

### 输出文件特点

1. **文件头信息**：包含源文件名和 Sheet 数量
2. **Sheet 分隔**：每个 Sheet 使用 `##` 标题和分隔线区分
3. **标题信息**：如果检测到标题行，会单独显示
4. **数据表格**：标准 Markdown 表格格式
5. **统计信息**：每个 Sheet 末尾显示数据行数和列数

## ⚙️ 工作原理

1. **读取 Excel 文件**：使用 pandas 读取所有工作表
2. **检测表头**：自动检测包含关键词（如 "SKU"、"商品"、"size"、"采购"、"数量"）的表头行
3. **提取标题**：如果表头不在第一行，提取前面的标题信息
4. **数据处理**：
   - 移除完全为空的行
   - 将 NaN 值替换为空字符串
   - 清理 "Unnamed" 列名
5. **生成 Markdown**：将数据转换为 Markdown 表格格式
6. **保存文件**：写入 UTF-8 编码的 Markdown 文件

## ⚠️ 注意事项

1. **文件路径**：支持相对路径和绝对路径，路径中包含空格时请使用引号
2. **文件编码**：输出文件使用 UTF-8 编码，确保正确显示中文
3. **表头检测**：脚本会自动检测表头，但如果 Excel 格式特殊，可能需要手动调整
4. **大文件处理**：对于非常大的 Excel 文件，转换可能需要一些时间
5. **图片处理**：Excel 中的图片公式（如 `=DISPIMG(...)`）会原样保留在 Markdown 中

## 🔍 常见问题

### Q1: 提示 "文件不存在"

**原因**：指定的 Excel 文件路径不正确或文件不存在

**解决方法**：
- 检查文件路径是否正确
- 使用绝对路径或确保相对路径正确
- 检查文件名是否正确（注意大小写和特殊字符）

### Q2: 提示 "ModuleNotFoundError: No module named 'pandas'"

**原因**：未安装必要的依赖包

**解决方法**：
```bash
pip install pandas openpyxl
```

### Q3: 转换后的表格格式不正确

**原因**：Excel 文件格式特殊，表头检测可能不准确

**解决方法**：
- 检查 Excel 文件，确保表头行包含关键词（如 "SKU"、"商品" 等）
- 如果表头在第一行，确保第一行包含这些关键词

### Q4: 中文显示乱码

**原因**：文件编码问题

**解决方法**：
- 确保输出文件使用 UTF-8 编码（脚本已自动处理）
- 使用支持 UTF-8 的编辑器打开 Markdown 文件

### Q5: 某些列显示为 "Unnamed"

**原因**：Excel 文件中某些列没有列名

**解决方法**：
- 脚本会自动清理 "Unnamed" 列，显示为空
- 如需保留列名，请在 Excel 中为所有列添加名称

## 📚 相关文件

- **脚本文件**：`convert_requirements_to_md.py`
- **依赖文件**：`OrderConverter/requirements.txt` 或 `xlsx_converter/requirements.txt`
- **示例输出**：`docs/requirements/LG-Le 1.16瑜伽裤 7天库存采购.md`

## 🔄 更新日志

### v1.0.0
- 初始版本
- 支持 Excel 转 Markdown 基本功能
- 自动检测表头
- 支持多 Sheet 处理
- 提取标题信息

## 📞 技术支持

如遇到问题或需要帮助，请检查：
1. Python 版本是否符合要求
2. 依赖包是否正确安装
3. Excel 文件格式是否标准
4. 文件路径是否正确

---

**最后更新**：2026-01-26
