#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
XLSX Converter Service (xlsx-converter)
订单处理服务 - 自动监控目录，处理订单文件
自动转换xlsx文件，对Orders目录进行特殊处理（添加列、计算cost/upsell等）
"""
import pandas as pd
import os
import sys
import time
from pathlib import Path
from datetime import datetime
import logging
import io
import codecs

# 设置控制台编码为UTF-8（解决Windows中文乱码问题）
if sys.platform == 'win32':
    try:
        # 设置环境变量
        os.environ['PYTHONIOENCODING'] = 'utf-8'
        # 设置标准输出编码为UTF-8
        if hasattr(sys.stdout, 'buffer'):
            sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace', line_buffering=True)
        if hasattr(sys.stderr, 'buffer'):
            sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8', errors='replace', line_buffering=True)
    except Exception as e:
        # 如果设置失败，继续运行
        pass

# 配置日志
# 创建自定义的StreamHandler，确保使用UTF-8编码
class UTF8StreamHandler(logging.StreamHandler):
    def __init__(self, stream=None):
        if stream is None:
            stream = sys.stdout
        # 如果是Windows且stream有buffer属性，使用UTF-8编码
        if sys.platform == 'win32' and hasattr(stream, 'buffer'):
            stream = io.TextIOWrapper(stream.buffer, encoding='utf-8', errors='replace', line_buffering=True)
        super().__init__(stream)

logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler(Path(__file__).parent / 'xlsx_converter.log', encoding='utf-8'),
        UTF8StreamHandler()
    ]
)
logger = logging.getLogger(__name__)

# 配置路径（相对于项目根目录）
BASE_DIR = Path(__file__).parent.parent  # 项目根目录
SOURCE_DIR = BASE_DIR / "docs" / "sourceData"
TARGET_DIR = BASE_DIR / "docs" / "convertedData"
CHECK_INTERVAL = 10  # 检查间隔（秒）

# 记录已处理的文件（避免重复转换）
processed_files = set()


def ensure_directories():
    """确保源目录和目标目录存在"""
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    TARGET_DIR.mkdir(parents=True, exist_ok=True)
    logger.info(f"源目录: {SOURCE_DIR.absolute()}")
    logger.info(f"目标目录: {TARGET_DIR.absolute()}")
    logger.info("模式: 递归扫描所有子目录，保持文件夹结构，转换后删除原文件")


def get_safe_filename(filename):
    """清理文件名，移除特殊字符"""
    # 移除扩展名
    name = Path(filename).stem
    # 只保留字母、数字、中文、空格、连字符和下划线
    safe_name = "".join(c for c in name if c.isalnum() or c in (' ', '-', '_', '（', '）', '（', '）'))
    return safe_name.replace(' ', '_')


def convert_xlsx_to_csv(xlsx_file_path, relative_path):
    """
    将xlsx文件的所有sheet转换为csv文件，并保持文件夹结构
    
    Args:
        xlsx_file_path: xlsx文件的完整路径
        relative_path: 相对于SOURCE_DIR的相对路径（用于保持文件夹结构）
        
    Returns:
        bool: 转换是否成功
    """
    xlsx_path = Path(xlsx_file_path)
    
    if not xlsx_path.exists():
        logger.warning(f"文件不存在: {xlsx_path}")
        return False
    
    # 计算目标目录和文件名
    relative_dir = Path(relative_path).parent
    target_subdir = TARGET_DIR / relative_dir
    base_name = get_safe_filename(xlsx_path.name)
    
    # 检查内存中的已处理记录（避免同一运行周期内重复处理相同文件）
    # 如果文件修改时间改变，说明是新的文件，需要重新转换并覆盖
    file_key = (str(relative_path), xlsx_path.stat().st_mtime)
    if file_key in processed_files:
        logger.debug(f"文件已处理（本周期内，相同修改时间），跳过: {relative_path}")
        return False
    
    try:
        logger.info(f"开始转换文件: {relative_path}")
        
        # 确保目标目录存在
        target_subdir.mkdir(parents=True, exist_ok=True)
        
        # 读取Excel文件
        excel_file = pd.ExcelFile(xlsx_path)
        sheet_names = excel_file.sheet_names
        
        logger.info(f"  发现 {len(sheet_names)} 个sheet")
        
        converted_files = []
        
        # 检查是否在Orders目录中
        is_orders_dir = "Orders" in str(relative_path) or "Orders" in str(relative_dir)
        
        # 转换每个sheet为CSV
        for sheet_name in sheet_names:
            try:
                # 读取sheet数据
                df = pd.read_excel(xlsx_path, sheet_name=sheet_name)
                
                # 跳过空sheet（如WPS内部sheet）
                if df.empty and ('WpsReserved' in sheet_name or 'Reserved' in sheet_name):
                    logger.debug(f"  跳过内部sheet: {sheet_name}")
                    continue
                
                # 生成输出文件名
                safe_sheet_name = get_safe_filename(sheet_name)
                if safe_sheet_name:
                    # Orders目录下的文件保存为Excel格式以支持货币格式和公式
                    if is_orders_dir:
                        output_filename = f"{base_name}_{safe_sheet_name}.xlsx"
                    else:
                        output_filename = f"{base_name}_{safe_sheet_name}.csv"
                else:
                    if is_orders_dir:
                        output_filename = f"{base_name}_sheet1.xlsx"
                    else:
                        output_filename = f"{base_name}_sheet1.csv"
                
                output_file = target_subdir / output_filename
                
                # 如果CSV文件已存在，记录日志（将覆盖）
                if output_file.exists():
                    logger.info(f"  覆盖已存在的文件: {relative_dir / output_filename}")
                
                # 如果是Orders目录下的文件，先保存为临时CSV进行数据处理
                temp_csv = None
                if is_orders_dir:
                    # 先保存为临时CSV文件进行处理
                    temp_csv = target_subdir / f"{output_file.stem}_temp.csv"
                    df.to_csv(temp_csv, index=False, encoding='utf-8-sig')
                    df_csv = pd.read_csv(temp_csv, encoding='utf-8-sig')
                else:
                    # 非Orders目录，直接保存为CSV
                    df.to_csv(output_file, index=False, encoding='utf-8-sig')
                    logger.info(f"  ✓ 已转换: {relative_dir / output_filename} ({len(df)} 行, {len(df.columns)} 列)")
                    converted_files.append(output_file)
                    continue  # 跳过后续处理
                
                # 如果是Orders目录下的文件，进行特殊处理
                if is_orders_dir:
                    try:
                        
                        # 检查是否有"商品SKU"列（优先匹配，支持列名中包含空格等）
                        sku_column = None
                        for col in df_csv.columns:
                            col_str = str(col).strip()
                            # 优先匹配"商品SKU"
                            if "商品SKU" in col_str:
                                sku_column = col
                                break
                        
                        # 如果没有找到"商品SKU"，尝试查找其他包含SKU的列
                        if sku_column is None:
                            for col in df_csv.columns:
                                col_str = str(col).strip()
                                if "SKU" in col_str.upper():
                                    sku_column = col
                                    break
                        
                        if sku_column is not None:
                            # 从商品SKU中提取SKU前缀（前两个部分，例如：YOG-001-Bleu-L -> YOG-001）
                            def extract_sku_prefix(sku_value):
                                """从SKU值中提取前缀（前两个部分，用连字符连接）"""
                                if pd.isna(sku_value) or sku_value == '':
                                    return ''
                                sku_str = str(sku_value).strip()
                                # 按连字符分割
                                parts = sku_str.split('-')
                                if len(parts) >= 2:
                                    # 如果有至少两个部分，返回前两个部分用连字符连接
                                    return f"{parts[0]}-{parts[1]}"
                                elif len(parts) == 1:
                                    # 如果只有一个部分，返回原值
                                    return parts[0]
                                # 如果没有部分，返回空字符串
                                return ''
                            
                            # 添加SKU_Quotation列（如果已存在则覆盖）
                            df_csv['SKU_Quotation'] = df_csv[sku_column].apply(extract_sku_prefix)
                            
                            # 重新排列列顺序：将商品SKU和SKU_Quotation都放在SKU列的右边
                            # 首先查找SKU列（不包含"商品"的SKU列）
                            base_sku_column = None
                            for col in df_csv.columns:
                                col_str = str(col).strip()
                                if "SKU" in col_str.upper() and "商品" not in col_str and "QUOTATION" not in col_str.upper():
                                    base_sku_column = col
                                    break
                            
                            # 如果找不到基础SKU列，使用商品SKU列作为参考
                            if base_sku_column is None:
                                base_sku_column = sku_column
                            
                            cols = list(df_csv.columns)
                            
                            # 移除需要重新排序的列
                            cols_to_reorder = []
                            if sku_column in cols and sku_column != base_sku_column:
                                cols_to_reorder.append(sku_column)
                                cols.remove(sku_column)
                            if 'SKU_Quotation' in cols:
                                cols_to_reorder.append('SKU_Quotation')
                                cols.remove('SKU_Quotation')
                            
                            # 找到基础SKU列的位置
                            if base_sku_column in cols:
                                sku_idx = cols.index(base_sku_column)
                                # 在SKU列后面依次插入商品SKU和SKU_Quotation
                                insert_idx = sku_idx + 1
                                for col_to_insert in cols_to_reorder:
                                    cols.insert(insert_idx, col_to_insert)
                                    insert_idx += 1
                                df_csv = df_csv[cols]
                            
                            logger.info(f"  已添加SKU_Quotation列（从'{sku_column}'列提取，位于SKU列右边）")
                            
                            # 添加QTY_Merged列
                            # 查找Order#列和QTY列
                            order_column = None
                            qty_column = None
                            
                            for col in df_csv.columns:
                                col_str = str(col).strip()
                                if "Order#" in col_str or ("Order" in col_str and "#" in col_str):
                                    order_column = col
                                if "QTY" in col_str.upper() and "MERGED" not in col_str.upper():
                                    qty_column = col
                            
                            if order_column is not None and qty_column is not None:
                                # 确保QTY列为数值类型
                                df_csv[qty_column] = pd.to_numeric(df_csv[qty_column], errors='coerce').fillna(0)
                                
                                # 按Order#和SKU_Quotation分组，计算QTY总和
                                # 创建分组键
                                df_csv['_group_key'] = df_csv[order_column].astype(str) + '_' + df_csv['SKU_Quotation'].astype(str)
                                
                                # 计算每组的QTY总和
                                qty_sum = df_csv.groupby('_group_key')[qty_column].transform('sum')
                                
                                # 初始化QTY_Merged列为空字符串
                                df_csv['QTY_Merged'] = ''
                                
                                # 对于每个分组，在第一行显示总和，其他行显示空
                                for group_key in df_csv['_group_key'].unique():
                                    group_mask = df_csv['_group_key'] == group_key
                                    group_indices = df_csv[group_mask].index.tolist()
                                    
                                    if len(group_indices) > 0:
                                        # 第一行显示总和（转换为整数，避免小数）
                                        first_idx = group_indices[0]
                                        sum_value = int(qty_sum.loc[first_idx]) if not pd.isna(qty_sum.loc[first_idx]) else ''
                                        df_csv.loc[first_idx, 'QTY_Merged'] = sum_value
                                        # 其他行保持为空（已经是空字符串）
                                
                                # 删除临时列
                                df_csv = df_csv.drop(columns=['_group_key'])
                                
                                # 重新排列列顺序：将QTY_Merged放在QTY列的右边
                                cols = list(df_csv.columns)
                                if 'QTY_Merged' in cols:
                                    cols.remove('QTY_Merged')
                                    # 找到QTY列的位置
                                    qty_idx = cols.index(qty_column) if qty_column in cols else len(cols)
                                    # 在QTY列后面插入QTY_Merged
                                    cols.insert(qty_idx + 1, 'QTY_Merged')
                                    df_csv = df_csv[cols]
                                
                                logger.info(f"  已添加QTY_Merged列（按Order#和SKU_Quotation分组累加QTY，位于QTY列右边）")
                                
                                # 添加cost和upsell列
                                # 查找Country列
                                country_column = None
                                for col in df_csv.columns:
                                    col_str = str(col).strip()
                                    if "Country" in col_str or "国家" in col_str:
                                        country_column = col
                                        break
                                
                                if country_column is not None and order_column is not None:
                                    # 初始化cost和upsell列
                                    df_csv['cost'] = ''
                                    df_csv['upsell'] = ''
                                    
                                    # 报价表文件路径
                                    quotation_dir = TARGET_DIR / "LG-Le" / "Quotation"
                                    
                                    # 加载报价表数据（只加载一次）
                                    quotation_df = None
                                    upsell_quotation_df = None
                                    
                                    logger.info(f"  查找报价表文件，目录: {quotation_dir}")
                                    logger.info(f"  目录是否存在: {quotation_dir.exists()}")
                                    
                                    if quotation_dir.exists():
                                        # 列出所有CSV文件用于调试
                                        all_csv_files = list(quotation_dir.glob("*.csv"))
                                        logger.info(f"  目录下所有CSV文件: {[f.name for f in all_csv_files]}")
                                        
                                        # 查找报价表文件（包含"报价表"但不包含"upsell"）
                                        quotation_files = list(quotation_dir.glob("*报价表.csv"))
                                        logger.info(f"  匹配'*报价表.csv'的文件: {[f.name for f in quotation_files]}")
                                        quotation_files = [f for f in quotation_files if "upsell" not in f.name.lower()]
                                        logger.info(f"  过滤后（不含upsell）的报价表文件: {[f.name for f in quotation_files]}")
                                        
                                        if quotation_files:
                                            try:
                                                quotation_df = pd.read_csv(quotation_files[0], encoding='utf-8-sig')
                                                logger.info(f"  已加载报价表文件: {quotation_files[0].name} (共{len(quotation_df)}行)")
                                            except Exception as e:
                                                logger.warning(f"  读取报价表文件失败: {str(e)}")
                                        else:
                                            logger.warning(f"  未找到报价表文件（包含'报价表'但不包含'upsell'）")
                                        
                                        # 查找upsell报价表文件（包含"upsell"和"报价表"）
                                        upsell_files = list(quotation_dir.glob("*upsell*报价表.csv"))
                                        upsell_files.extend(list(quotation_dir.glob("*报价表*upsell*.csv")))
                                        logger.info(f"  匹配upsell报价表文件: {[f.name for f in upsell_files]}")
                                        
                                        if upsell_files:
                                            try:
                                                upsell_quotation_df = pd.read_csv(upsell_files[0], encoding='utf-8-sig')
                                                logger.info(f"  已加载upsell报价表文件: {upsell_files[0].name} (共{len(upsell_quotation_df)}行)")
                                            except Exception as e:
                                                logger.warning(f"  读取upsell报价表文件失败: {str(e)}")
                                        else:
                                            logger.warning(f"  未找到upsell报价表文件（包含'upsell'和'报价表'）")
                                    else:
                                        logger.warning(f"  报价表目录不存在: {quotation_dir}")
                                    
                                    # 按Order#分组处理
                                    for order_num in df_csv[order_column].unique():
                                        order_mask = df_csv[order_column] == order_num
                                        order_rows = df_csv[order_mask].copy()
                                        
                                        # 找到该Order#中所有有QTY_Merged值的行
                                        rows_with_qty = []
                                        for idx in order_rows.index:
                                            qty_merged_val = order_rows.loc[idx, 'QTY_Merged']
                                            if qty_merged_val != '' and pd.notna(qty_merged_val) and str(qty_merged_val).strip() != '':
                                                try:
                                                    qty_val = int(float(str(qty_merged_val)))
                                                    rows_with_qty.append((idx, qty_val))
                                                except:
                                                    pass
                                        
                                        if len(rows_with_qty) == 0:
                                            # 如果QTY_Merged没有值，cost和upsell都为空
                                            continue
                                        
                                        # 找到QTY_Merged最大的行（设置cost）
                                        max_qty_row = max(rows_with_qty, key=lambda x: x[1])
                                        max_idx = max_qty_row[0]
                                        max_qty_merged = max_qty_row[1]
                                        
                                        # 获取最大QTY_Merged行的信息
                                        sku_quotation_max = df_csv.loc[max_idx, 'SKU_Quotation']
                                        country_max = df_csv.loc[max_idx, country_column]
                                        
                                        if pd.isna(sku_quotation_max) or str(sku_quotation_max).strip() == '':
                                            continue
                                        if pd.isna(country_max) or str(country_max).strip() == '':
                                            continue
                                        
                                        country_code = str(country_max).strip().upper()
                                        
                                        # 为最大QTY_Merged的行设置cost值
                                        if quotation_df is not None:
                                            # 查找SKU_Quotation匹配的行
                                            sku_cols = [col for col in quotation_df.columns if 'SKU' in str(col).upper()]
                                            match_rows = pd.DataFrame()
                                            
                                            for sku_col in sku_cols:
                                                matches = quotation_df[quotation_df[sku_col].astype(str).str.strip() == str(sku_quotation_max).strip()]
                                                if len(matches) > 0:
                                                    match_rows = matches
                                                    break
                                            
                                            if len(match_rows) > 0:
                                                # 查找QTY匹配的行
                                                qty_cols = [col for col in match_rows.columns if 'QTY' in str(col).upper() and 'MERGED' not in str(col).upper()]
                                                found_cost = False
                                                
                                                for _, row in match_rows.iterrows():
                                                    # 检查QTY是否匹配
                                                    qty_match = False
                                                    for qty_col in qty_cols:
                                                        try:
                                                            row_qty = int(float(str(row[qty_col])))
                                                            if row_qty == max_qty_merged:
                                                                qty_match = True
                                                                break
                                                        except:
                                                            pass
                                                    
                                                    if qty_match:
                                                        # 查找包含Country代码的列
                                                        country_cols = [col for col in match_rows.columns if country_code in str(col).upper()]
                                                        if country_cols:
                                                            cost_value = row[country_cols[0]]
                                                            if pd.notna(cost_value):
                                                                try:
                                                                    # 存储数值（Excel打开时会自动应用格式）
                                                                    cost_num = float(cost_value)
                                                                    df_csv.loc[max_idx, 'cost'] = cost_num
                                                                except:
                                                                    df_csv.loc[max_idx, 'cost'] = cost_value
                                                                found_cost = True
                                                                break
                                                
                                                if not found_cost:
                                                    df_csv.loc[max_idx, 'cost'] = f'未找到QTY={max_qty_merged}或Country={country_code}的数据'
                                            else:
                                                df_csv.loc[max_idx, 'cost'] = f'未找到SKU_Quotation={sku_quotation_max}的数据'
                                        else:
                                            df_csv.loc[max_idx, 'cost'] = '报价表文件不存在'
                                        
                                        # 为其他有QTY_Merged值的行设置upsell值
                                        other_rows = [r for r in rows_with_qty if r[0] != max_idx]
                                        
                                        for other_idx, other_qty in other_rows:
                                            other_sku_quotation = df_csv.loc[other_idx, 'SKU_Quotation']
                                            other_country = df_csv.loc[other_idx, country_column]
                                            
                                            if pd.isna(other_sku_quotation) or str(other_sku_quotation).strip() == '':
                                                continue
                                            if pd.isna(other_country) or str(other_country).strip() == '':
                                                continue
                                            
                                            other_country_code = str(other_country).strip().upper()
                                            
                                            # 计算upsell值
                                            if upsell_quotation_df is not None:
                                                # 查找SKU_Quotation匹配的行
                                                sku_cols = [col for col in upsell_quotation_df.columns if 'SKU' in str(col).upper()]
                                                match_rows = pd.DataFrame()
                                                
                                                for sku_col in sku_cols:
                                                    matches = upsell_quotation_df[upsell_quotation_df[sku_col].astype(str).str.strip() == str(other_sku_quotation).strip()]
                                                    if len(matches) > 0:
                                                        match_rows = matches
                                                        break
                                                
                                                if len(match_rows) > 0:
                                                    # 查找QTY匹配的行
                                                    qty_cols = [col for col in match_rows.columns if 'QTY' in str(col).upper() and 'MERGED' not in str(col).upper()]
                                                    found_upsell = False
                                                    
                                                    for _, row in match_rows.iterrows():
                                                        # 检查QTY是否匹配
                                                        qty_match = False
                                                        for qty_col in qty_cols:
                                                            try:
                                                                row_qty = int(float(str(row[qty_col])))
                                                                if row_qty == other_qty:
                                                                    qty_match = True
                                                                    break
                                                            except:
                                                                pass
                                                        
                                                        if qty_match:
                                                            # 查找包含Country代码的列
                                                            country_cols = [col for col in match_rows.columns if other_country_code in str(col).upper()]
                                                            if country_cols:
                                                                upsell_value = row[country_cols[0]]
                                                                if pd.notna(upsell_value):
                                                                    try:
                                                                        # 存储数值（Excel打开时会自动应用格式）
                                                                        upsell_num = float(upsell_value)
                                                                        df_csv.loc[other_idx, 'upsell'] = upsell_num
                                                                    except:
                                                                        df_csv.loc[other_idx, 'upsell'] = upsell_value
                                                                    found_upsell = True
                                                                    break
                                                
                                                if not found_upsell:
                                                    df_csv.loc[other_idx, 'upsell'] = f'未找到QTY={other_qty}或Country={other_country_code}的数据'
                                            else:
                                                df_csv.loc[other_idx, 'upsell'] = 'upsell报价表文件不存在'
                                    
                                    # 重新排列列顺序：将cost和upsell放在Country列的左边
                                    cols = list(df_csv.columns)
                                    if 'cost' in cols and 'upsell' in cols and country_column in cols:
                                        cols.remove('cost')
                                        cols.remove('upsell')
                                        country_idx = cols.index(country_column)
                                        cols.insert(country_idx, 'upsell')
                                        cols.insert(country_idx, 'cost')
                                        df_csv = df_csv[cols]
                                    
                                    # 在最后一行添加sum行（不创建sum列，只在最后一行显示sum文本和值）
                                    # 找到cost列和upsell列的位置（在重新排列列顺序之后）
                                    cost_idx = list(df_csv.columns).index('cost') if 'cost' in df_csv.columns else -1
                                    upsell_idx = list(df_csv.columns).index('upsell') if 'upsell' in df_csv.columns else -1
                                    
                                    # 找到cost列左边的列（用于显示"sum"文本）
                                    sum_text_col_idx = -1
                                    if cost_idx >= 0:
                                        # 如果cost列不是第一列，使用cost列左边的列
                                        if cost_idx > 0:
                                            sum_text_col_idx = cost_idx - 1
                                        else:
                                            # 如果cost列是第一列，使用第一列
                                            sum_text_col_idx = 0
                                    
                                    # 创建新行（最后一行）
                                    new_row = pd.Series(index=df_csv.columns, dtype=object)
                                    new_row_index = len(df_csv)
                                    df_csv.loc[new_row_index] = new_row
                                    
                                    # 在cost列左边的列显示"sum"文本（不创建新列，直接设置值）
                                    if sum_text_col_idx >= 0:
                                        sum_text_col_name = df_csv.columns[sum_text_col_idx]
                                        df_csv.loc[new_row_index, sum_text_col_name] = 'sum'
                                    
                                    # 设置cost列的值（使用公式求和）
                                    if 'cost' in df_csv.columns and 'upsell' in df_csv.columns and cost_idx >= 0 and upsell_idx >= 0:
                                        # 计算cost和upsell列的数据范围
                                        # Excel行号从1开始，第1行是标题，数据从第2行开始
                                        data_start_row = 2
                                        data_end_row = new_row_index + 1  # 最后一行数据（不包括sum行）
                                        
                                        # 将列索引转换为Excel列字母（A, B, C, ...）
                                        def col_index_to_letter(n):
                                            """将列索引转换为Excel列字母（0=A, 1=B, ...）"""
                                            result = ""
                                            while n >= 0:
                                                result = chr(65 + (n % 26)) + result
                                                n = n // 26 - 1
                                            return result
                                        
                                        cost_col_letter = col_index_to_letter(cost_idx)
                                        upsell_col_letter = col_index_to_letter(upsell_idx)
                                        
                                        # 生成公式：SUM(cost列范围)+SUM(upsell列范围)
                                        # 例如：=SUM(C2:C10)+SUM(D2:D10)
                                        cost_formula = f"=SUM({cost_col_letter}{data_start_row}:{cost_col_letter}{data_end_row})+SUM({upsell_col_letter}{data_start_row}:{upsell_col_letter}{data_end_row})"
                                        df_csv.loc[new_row_index, 'cost'] = cost_formula
                                        
                                        logger.info(f"  已添加sum行，公式: {cost_formula}")
                                    
                                    logger.info(f"  已添加cost和upsell列（位于Country列左边），并在最后一行添加sum行（使用公式计算）")
                                else:
                                    if country_column is None:
                                        logger.warning(f"  未找到Country列，跳过添加cost和upsell列")
                            else:
                                missing_cols = []
                                if order_column is None:
                                    missing_cols.append("Order#")
                                if qty_column is None:
                                    missing_cols.append("QTY")
                                logger.warning(f"  未找到{', '.join(missing_cols)}列，跳过添加QTY_Merged列")
                            
                            # 保存文件（Orders目录保存为Excel格式，其他保存为CSV）
                            if is_orders_dir:
                                # 保存为Excel格式以支持货币格式和公式
                                from openpyxl import Workbook
                                from openpyxl.styles import Font, PatternFill
                                from openpyxl.utils import get_column_letter
                                from openpyxl.utils.dataframe import dataframe_to_rows
                                
                                wb = Workbook()
                                ws = wb.active
                                ws.title = safe_sheet_name if safe_sheet_name else "Sheet1"
                                
                                # 冻结表头（第一行）
                                ws.freeze_panes = 'A2'
                                
                                # 写入数据
                                cost_col_idx = None
                                upsell_col_idx = None
                                
                                for r_idx, row in enumerate(dataframe_to_rows(df_csv, index=False, header=True), 1):
                                    for c_idx, value in enumerate(row, 1):
                                        if c_idx > len(df_csv.columns):
                                            break
                                        col_name = df_csv.columns[c_idx - 1]
                                        cell = ws.cell(row=r_idx, column=c_idx, value=value)
                                        
                                        # 记录cost和upsell列的位置
                                        if r_idx == 1:  # 标题行
                                            if col_name == 'cost':
                                                cost_col_idx = c_idx
                                            elif col_name == 'upsell':
                                                upsell_col_idx = c_idx
                                        
                                        # 如果是cost或upsell列，设置货币格式（US$，两位小数）
                                        if col_name in ['cost', 'upsell']:
                                            # 设置货币格式为US$，保留两位小数（所有单元格，包括公式和数值）
                                            cell.number_format = '$#,##0.00'
                                            
                                            # 如果是错误信息（包含"未找到"等），设置为红色背景
                                            if isinstance(value, str) and ('未找到' in value or '不存在' in value):
                                                cell.fill = PatternFill(start_color="FF0000", end_color="FF0000", fill_type="solid")
                                                cell.font = Font(color="FFFFFF")  # 白色文字
                                        
                                        # 如果是其他列的错误信息，也设置为红色
                                        elif isinstance(value, str) and ('未找到' in value or '不存在' in value):
                                            cell.fill = PatternFill(start_color="FF0000", end_color="FF0000", fill_type="solid")
                                            cell.font = Font(color="FFFFFF")  # 白色文字
                                
                                # 为整个cost和upsell列设置货币格式（确保所有单元格都有格式，包括空单元格）
                                if cost_col_idx:
                                    for row in ws.iter_rows(min_row=2, max_row=ws.max_row, min_col=cost_col_idx, max_col=cost_col_idx):
                                        for cell in row:
                                            # 为所有单元格设置格式（包括空单元格、数值、公式）
                                            cell.number_format = '$#,##0.00'
                                
                                if upsell_col_idx:
                                    for row in ws.iter_rows(min_row=2, max_row=ws.max_row, min_col=upsell_col_idx, max_col=upsell_col_idx):
                                        for cell in row:
                                            # 为所有单元格设置格式（包括空单元格、数值、公式）
                                            cell.number_format = '$#,##0.00'
                                
                                # 高亮显示最后一行（sum行）
                                if ws.max_row > 1:
                                    last_row = ws.max_row
                                    # 找到显示"sum"文本的列和cost列
                                    sum_text_col_idx = None
                                    for c_idx in range(1, ws.max_column + 1):
                                        cell = ws.cell(row=last_row, column=c_idx)
                                        if cell.value == 'sum':
                                            sum_text_col_idx = c_idx
                                            break
                                    
                                    # 高亮显示最后一行
                                    from openpyxl.styles import Alignment
                                    highlight_fill = PatternFill(start_color="FFFF00", end_color="FFFF00", fill_type="solid")  # 黄色背景
                                    highlight_font = Font(bold=True)  # 粗体
                                    
                                    # 高亮显示sum文本单元格
                                    if sum_text_col_idx:
                                        sum_cell = ws.cell(row=last_row, column=sum_text_col_idx)
                                        sum_cell.fill = highlight_fill
                                        sum_cell.font = highlight_font
                                        sum_cell.alignment = Alignment(horizontal='center')
                                    
                                    # 高亮显示cost列（最后一行）
                                    if cost_col_idx:
                                        cost_cell = ws.cell(row=last_row, column=cost_col_idx)
                                        cost_cell.fill = highlight_fill
                                        cost_cell.font = highlight_font
                                
                                # 保存Excel文件
                                wb.save(output_file)
                                
                                # 关闭工作簿，释放文件句柄
                                wb.close()
                                
                                # 删除临时CSV文件
                                if temp_csv.exists():
                                    try:
                                        temp_csv.unlink()
                                    except Exception as e:
                                        logger.warning(f"  删除临时CSV文件失败: {str(e)}")
                                
                                logger.info(f"  已保存为Excel格式（支持货币格式和公式）")
                            else:
                                # 保存为CSV格式
                                df_csv.to_csv(output_file, index=False, encoding='utf-8-sig')
                        else:
                            logger.warning(f"  未找到商品SKU列，跳过添加SKU_Quotation列")
                            # 即使没有商品SKU列，也要保存文件
                            if is_orders_dir:
                                # 保存为Excel格式
                                from openpyxl import Workbook
                                from openpyxl.utils.dataframe import dataframe_to_rows
                                
                                wb = Workbook()
                                ws = wb.active
                                ws.title = safe_sheet_name if safe_sheet_name else "Sheet1"
                                
                                # 冻结表头（第一行）
                                ws.freeze_panes = 'A2'
                                
                                # 写入数据
                                for r_idx, row in enumerate(dataframe_to_rows(df_csv, index=False, header=True), 1):
                                    ws.append(row)
                                
                                # 调整列宽
                                from openpyxl.utils import get_column_letter
                                for col in ws.columns:
                                    max_length = 0
                                    column = col[0].column
                                    for cell in col:
                                        try:
                                            if len(str(cell.value)) > max_length:
                                                max_length = len(str(cell.value))
                                        except:
                                            pass
                                    adjusted_width = (max_length + 2)
                                    ws.column_dimensions[get_column_letter(column)].width = adjusted_width
                                
                                wb.save(output_file)
                                
                                # 删除临时CSV文件
                                if temp_csv.exists():
                                    temp_csv.unlink()
                                
                                logger.info(f"  已保存为Excel格式（未添加SKU_Quotation列）")
                            else:
                                # 保存为CSV格式
                                df_csv.to_csv(output_file, index=False, encoding='utf-8-sig')
                    except Exception as e:
                        logger.error(f"  处理SKU_Quotation列时出错: {str(e)}")
                        # 即使出错，也要尝试保存文件
                        try:
                            if is_orders_dir:
                                # 保存为Excel格式
                                from openpyxl import Workbook
                                from openpyxl.utils.dataframe import dataframe_to_rows
                                
                                wb = Workbook()
                                ws = wb.active
                                ws.title = safe_sheet_name if safe_sheet_name else "Sheet1"
                                
                                # 冻结表头（第一行）
                                ws.freeze_panes = 'A2'
                                
                                # 写入数据
                                for r_idx, row in enumerate(dataframe_to_rows(df_csv, index=False, header=True), 1):
                                    ws.append(row)
                                
                                # 调整列宽
                                from openpyxl.utils import get_column_letter
                                for col in ws.columns:
                                    max_length = 0
                                    column = col[0].column
                                    for cell in col:
                                        try:
                                            if len(str(cell.value)) > max_length:
                                                max_length = len(str(cell.value))
                                        except:
                                            pass
                                    adjusted_width = (max_length + 2)
                                    ws.column_dimensions[get_column_letter(column)].width = adjusted_width
                                
                                wb.save(output_file)
                                
                                # 删除临时CSV文件
                                if temp_csv.exists():
                                    temp_csv.unlink()
                                
                                logger.info(f"  已保存为Excel格式（处理出错，但已保存原始数据）")
                            else:
                                # 保存为CSV格式
                                df_csv.to_csv(output_file, index=False, encoding='utf-8-sig')
                        except Exception as e2:
                            logger.error(f"  保存文件时出错: {str(e2)}")
                
                logger.info(f"  ✓ 已转换: {relative_dir / output_filename} ({len(df)} 行, {len(df.columns)} 列)")
                converted_files.append(output_file)
                
            except Exception as e:
                logger.error(f"  转换sheet '{sheet_name}' 时出错: {str(e)}")
                continue
        
        # 如果转换成功，删除原文件
        if converted_files:
            # 确保ExcelFile对象已关闭，释放文件句柄
            if 'excel_file' in locals():
                excel_file.close()
            
            # 尝试删除原文件，如果失败则重试
            deleted = False
            max_retries = 5  # 增加重试次数
            retry_delay = 1.0  # 重试延迟时间
            for attempt in range(max_retries):
                try:
                    # 等待一小段时间，确保文件句柄已释放
                    if attempt > 0:
                        time.sleep(0.5)
                    
                    xlsx_path.unlink()  # 删除原xlsx文件
                    logger.info(f"✓ 已删除原文件: {relative_path}")
                    deleted = True
                    break
                except PermissionError as e:
                    if attempt < max_retries - 1:
                        logger.warning(f"删除原文件失败（文件可能被占用），0.5秒后重试 ({attempt + 1}/{max_retries}): {relative_path}")
                    else:
                        logger.error(f"删除原文件失败（文件被占用，已重试{max_retries}次）: {relative_path} - {str(e)}")
                except Exception as e:
                    logger.error(f"删除原文件失败: {relative_path} - {str(e)}")
                    break
            
            # 标记文件已处理
            processed_files.add(file_key)
            
            if deleted:
                logger.info(f"✓ 文件转换完成并已删除: {relative_path} -> {len(converted_files)} 个CSV文件")
            else:
                logger.warning(f"✓ 文件转换完成但删除失败: {relative_path} -> {len(converted_files)} 个CSV文件")
            
            return True
        else:
            logger.warning(f"文件转换完成，但未生成任何CSV: {relative_path}")
            return False
        
    except Exception as e:
        logger.error(f"转换文件时出错{relative_path}: {str(e)}")
        return False


def sync_directory_structure():
    """同步convertedData的目录结构，使其与sourceData保持一致"""
    if not SOURCE_DIR.exists():
        return
    
    try:
        # 遍历sourceData的所有目录，在convertedData中创建对应的目录结构
        for source_dir in SOURCE_DIR.rglob("*"):
            if source_dir.is_dir():
                # 计算相对路径
                try:
                    relative_dir = source_dir.relative_to(SOURCE_DIR)
                    target_dir = TARGET_DIR / relative_dir
                    # 创建对应的目标目录（如果不存在）
                    target_dir.mkdir(parents=True, exist_ok=True)
                except ValueError:
                    continue
    except Exception as e:
        logger.debug(f"同步目录结构时出错: {str(e)}")


def scan_and_convert():
    """递归扫描源目录及其所有子目录，转换所有xlsx文件"""
    if not SOURCE_DIR.exists():
        logger.warning(f"源目录不存在: {SOURCE_DIR}")
        return
    
    # 先同步目录结构
    sync_directory_structure()
    
    # 递归查找所有xlsx文件（包括子目录）
    xlsx_files = list(SOURCE_DIR.rglob("*.xlsx"))
    xlsx_files.extend(SOURCE_DIR.rglob("*.XLSX"))  # 处理大写扩展名
    
    if not xlsx_files:
        logger.debug("未发现新的xlsx文件")
        return
    
    logger.info(f"发现 {len(xlsx_files)} 个xlsx文件")
    
    for xlsx_file in xlsx_files:
        # 计算相对于SOURCE_DIR的相对路径
        try:
            relative_path = xlsx_file.relative_to(SOURCE_DIR)
            convert_xlsx_to_csv(xlsx_file, relative_path)
        except ValueError:
            # 如果无法计算相对路径，使用文件名
            convert_xlsx_to_csv(xlsx_file, xlsx_file.name)


def main():
    """主函数"""
    logger.info("=" * 60)
    logger.info("XLSX Converter Service (xlsx-converter) 启动")
    logger.info("=" * 60)
    
    ensure_directories()
    
    logger.info(f"监控间隔: {CHECK_INTERVAL} 秒")
    logger.info("程序运行中，按Ctrl+C 停止...")
    logger.info("")
    
    try:
        while True:
            scan_and_convert()
            logger.debug(f"等待 {CHECK_INTERVAL} 秒后进行下次检查...")
            time.sleep(CHECK_INTERVAL)
            
    except KeyboardInterrupt:
        logger.info("")
        logger.info("程序被用户中断，正在退出...")
    except Exception as e:
        logger.error(f"程序运行出错: {str(e)}", exc_info=True)
    finally:
        logger.info("程序已停止")


if __name__ == "__main__":
    main()
