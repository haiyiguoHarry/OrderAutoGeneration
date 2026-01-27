# OrderAutoGeneration 项目

订单自动生成系统

## 项目结构

```
OrderAutoGeneration/
├── docs/                    # 文档和数据目录
│   ├── requirements/        # 需求文档
│   ├── sourceData/          # 源数据目录（xlsx文件）
│   └── readyData/           # 处理后的数据目录（csv文件）
├── xlsx_converter/          # XLSX转CSV转换工具
│   ├── xlsx_to_csv_converter.py
│   ├── requirements.txt
│   └── README.md
└── README.md                # 本文件
```

## 工具程序

### XLSX 转 CSV 转换器

位置：`xlsx_converter/`

自动监控 `docs/sourceData` 目录，将 Excel 文件转换为 CSV 格式。

详细说明请查看：`xlsx_converter/README.md`

## 使用说明

每个工具程序都有独立的目录和说明文档，请进入对应目录查看具体使用方法。
