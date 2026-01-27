#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
将Excel需求文件转换为Markdown格式
"""
import pandas as pd
import sys
from pathlib import Path

def excel_to_markdown(excel_path, output_path=None):
    """
    将Excel文件转换为Markdown格式
    
    Args:
        excel_path: Excel文件路径
        output_path: 输出Markdown文件路径，如果为None则自动生成
    """
    excel_path = Path(excel_path)
    
    if not excel_path.exists():
        print(f"错误：文件不存在 {excel_path}")
        return False
    
    # 如果没有指定输出路径，自动生成
    if output_path is None:
        output_path = excel_path.parent / f"{excel_path.stem}.md"
    else:
        output_path = Path(output_path)
    
    try:
        # 读取Excel文件的所有sheet
        excel_file = pd.ExcelFile(excel_path)
        sheet_names = excel_file.sheet_names
        
        # 生成Markdown内容
        md_content = []
        md_content.append(f"# {excel_path.stem}\n")
        md_content.append(f"**源文件**: `{excel_path.name}`\n")
        md_content.append(f"**Sheet数量**: {len(sheet_names)}\n\n")
        md_content.append("---\n\n")
        
        # 处理每个sheet
        for idx, sheet_name in enumerate(sheet_names, 1):
            print(f"正在处理 Sheet {idx}/{len(sheet_names)}: {sheet_name}")
            
            # 读取sheet数据，先读取前几行来判断表头位置
            df_raw = pd.read_excel(excel_file, sheet_name=sheet_name, header=None, nrows=5)
            
            # 添加sheet标题
            md_content.append(f"## Sheet {idx}: {sheet_name}\n\n")
            
            # 如果数据为空
            if df_raw.empty:
                md_content.append("*（此Sheet为空）*\n\n")
                continue
            
            # 尝试自动检测表头行（通常是第一行或第二行包含"SKU"等关键字段）
            header_row = 0
            for i in range(min(3, len(df_raw))):
                row_values = [str(val).lower() for val in df_raw.iloc[i].values if pd.notna(val)]
                # 检查是否包含常见的表头关键词
                if any(keyword in ' '.join(row_values) for keyword in ['sku', '商品', 'size', '采购', '数量']):
                    header_row = i
                    break
            
            # 如果第一行是标题信息，保存它
            title_info = None
            if header_row > 0:
                title_row = pd.read_excel(excel_file, sheet_name=sheet_name, header=None, nrows=header_row)
                title_values = []
                for row_idx in range(header_row):
                    row_vals = [str(val) for val in title_row.iloc[row_idx].values if pd.notna(val) and str(val).strip()]
                    if row_vals:
                        title_values.extend(row_vals)
                if title_values:
                    title_info = ' | '.join(title_values)
                    md_content.append(f"**标题信息**: {title_info}\n\n")
            
            # 重新读取，使用检测到的表头行
            df = pd.read_excel(excel_file, sheet_name=sheet_name, header=header_row)
            
            # 移除完全为空的行
            df = df.dropna(how='all')
            
            # 处理数据
            # 替换NaN为空字符串
            df = df.fillna('')
            
            # 清理列名（移除Unnamed列）
            df.columns = [col if not str(col).startswith('Unnamed') else '' for col in df.columns]
            
            # 转换为Markdown表格
            # 获取表头
            headers = df.columns.tolist()
            
            # 生成Markdown表格
            md_content.append("### 数据表格\n\n")
            
            # 表头
            header_row_md = "| " + " | ".join(str(h) if h else ' ' for h in headers) + " |"
            md_content.append(header_row_md + "\n")
            
            # 分隔线
            separator = "| " + " | ".join(["---"] * len(headers)) + " |"
            md_content.append(separator + "\n")
            
            # 数据行
            for _, row in df.iterrows():
                row_data = "| " + " | ".join(str(val) if val else ' ' for val in row.values) + " |"
                md_content.append(row_data + "\n")
            
            md_content.append("\n")
            
            # 添加数据统计信息
            md_content.append(f"**数据行数**: {len(df)}\n")
            md_content.append(f"**列数**: {len(df.columns)}\n\n")
            
            # 如果有多个sheet，添加分隔符
            if idx < len(sheet_names):
                md_content.append("---\n\n")
        
        # 写入文件
        with open(output_path, 'w', encoding='utf-8') as f:
            f.write(''.join(md_content))
        
        print(f"\n转换完成！")
        print(f"输出文件: {output_path}")
        return True
        
    except Exception as e:
        print(f"错误：转换失败 - {str(e)}")
        import traceback
        traceback.print_exc()
        return False

if __name__ == "__main__":
    # 默认处理指定文件
    excel_file = r"e:\workspace\OrderAutoGeneration\docs\requirements\LG-Le 1.16瑜伽裤 7天库存采购.xlsx"
    
    if len(sys.argv) > 1:
        excel_file = sys.argv[1]
    
    output_file = None
    if len(sys.argv) > 2:
        output_file = sys.argv[2]
    
    excel_to_markdown(excel_file, output_file)
