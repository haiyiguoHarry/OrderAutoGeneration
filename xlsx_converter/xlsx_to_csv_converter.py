#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
XLSX Converter Service (xlsx-converter)
璁㈠崟澶勭悊鏈嶅姟 - 鑷姩鐩戞帶鐩綍锛屽鐞嗚鍗曟枃浠?
鑷姩杞崲xlsx鏂囦欢锛屽Orders鐩綍杩涜鐗规畩澶勭悊锛堟坊鍔犲垪銆佽绠梒ost/upsell绛夛級
"""
import pandas as pd
import os
import time
from pathlib import Path
from datetime import datetime
import logging

# 配置日志
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler(Path(__file__).parent / 'xlsx_converter.log', encoding='utf-8'),
        logging.StreamHandler()
    ]
)
logger = logging.getLogger(__name__)

# 閰嶇疆璺緞锛堢浉瀵逛簬椤圭洰鏍圭洰褰曪級
BASE_DIR = Path(__file__).parent.parent  # 椤圭洰鏍圭洰褰?
SOURCE_DIR = BASE_DIR / "docs" / "sourceData"
TARGET_DIR = BASE_DIR / "docs" / "convertedData"
CHECK_INTERVAL = 10  # 检查间隔（秒）

# 璁板綍宸插鐞嗙殑鏂囦欢锛堥伩鍏嶉噸澶嶈浆鎹級
processed_files = set()


def ensure_directories():
    """纭繚婧愮洰褰曞拰鐩爣鐩綍瀛樺湪"""
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    TARGET_DIR.mkdir(parents=True, exist_ok=True)
    logger.info(f"婧愮洰褰? {SOURCE_DIR.absolute()}")
    logger.info(f"鐩爣鐩綍: {TARGET_DIR.absolute()}")
    logger.info("妯″紡: 閫掑綊鎵弿鎵€鏈夊瓙鐩綍锛屼繚鎸佹枃浠跺す缁撴瀯锛岃浆鎹㈠悗鍒犻櫎鍘熸枃浠?)


def get_safe_filename(filename):
    """娓呯悊鏂囦欢鍚嶏紝绉婚櫎鐗规畩瀛楃"""
    # 绉婚櫎鎵╁睍鍚?
    name = Path(filename).stem
    # 鍙繚鐣欏瓧姣嶃€佹暟瀛椼€佷腑鏂囥€佺┖鏍笺€佽繛瀛楃鍜屼笅鍒掔嚎
    safe_name = "".join(c for c in name if c.isalnum() or c in (' ', '-', '_', '锛?, '銆?, '锛?, '锛?))
    return safe_name.replace(' ', '_')


def convert_xlsx_to_csv(xlsx_file_path, relative_path):
    """
    灏唜lsx鏂囦欢鐨勬墍鏈塻heet杞崲涓篶sv鏂囦欢锛屽苟淇濇寔鏂囦欢澶圭粨鏋?
    
    Args:
        xlsx_file_path: xlsx鏂囦欢鐨勫畬鏁磋矾寰?
        relative_path: 鐩稿浜嶴OURCE_DIR鐨勭浉瀵硅矾寰勶紙鐢ㄤ簬淇濇寔鏂囦欢澶圭粨鏋勶級
        
    Returns:
        bool: 杞崲鏄惁鎴愬姛
    """
    xlsx_path = Path(xlsx_file_path)
    
    if not xlsx_path.exists():
        logger.warning(f"鏂囦欢涓嶅瓨鍦? {xlsx_path}")
        return False
    
    # 璁＄畻鐩爣鐩綍鍜屾枃浠跺悕
    relative_dir = Path(relative_path).parent
    target_subdir = TARGET_DIR / relative_dir
    base_name = get_safe_filename(xlsx_path.name)
    
    # 妫€鏌ュ唴瀛樹腑鐨勫凡澶勭悊璁板綍锛堥伩鍏嶅悓涓€杩愯鍛ㄦ湡鍐呴噸澶嶅鐞嗙浉鍚屾枃浠讹級
    # 濡傛灉鏂囦欢淇敼鏃堕棿鏀瑰彉锛岃鏄庢槸鏂扮殑鏂囦欢锛岄渶瑕侀噸鏂拌浆鎹㈠苟瑕嗙洊
    file_key = (str(relative_path), xlsx_path.stat().st_mtime)
    if file_key in processed_files:
        logger.debug(f"鏂囦欢宸插鐞嗭紙鏈懆鏈熷唴锛岀浉鍚屼慨鏀规椂闂达級锛岃烦杩? {relative_path}")
        return False
    
    try:
        logger.info(f"寮€濮嬭浆鎹㈡枃浠? {relative_path}")
        
        # 纭繚鐩爣鐩綍瀛樺湪
        target_subdir.mkdir(parents=True, exist_ok=True)
        
        # 璇诲彇Excel鏂囦欢
        excel_file = pd.ExcelFile(xlsx_path)
        sheet_names = excel_file.sheet_names
        
        logger.info(f"  鍙戠幇 {len(sheet_names)} 涓猻heet")
        
        converted_files = []
        
        # 妫€鏌ユ槸鍚﹀湪Orders鐩綍涓?
        is_orders_dir = "Orders" in str(relative_path) or "Orders" in str(relative_dir)
        
        # 杞崲姣忎釜sheet涓篊SV
        for sheet_name in sheet_names:
            try:
                # 璇诲彇sheet鏁版嵁
                df = pd.read_excel(xlsx_path, sheet_name=sheet_name)
                
                # 璺宠繃绌簊heet锛堝WPS鍐呴儴sheet锛?
                if df.empty and ('WpsReserved' in sheet_name or 'Reserved' in sheet_name):
                    logger.debug(f"  璺宠繃鍐呴儴sheet: {sheet_name}")
                    continue
                
                # 鐢熸垚杈撳嚭鏂囦欢鍚?
                safe_sheet_name = get_safe_filename(sheet_name)
                if safe_sheet_name:
                    # Orders鐩綍涓嬬殑鏂囦欢淇濆瓨涓篍xcel鏍煎紡浠ユ敮鎸佽揣甯佹牸寮忓拰鍏紡
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
                
                # 濡傛灉CSV鏂囦欢宸插瓨鍦紝璁板綍鏃ュ織锛堝皢瑕嗙洊锛?
                if output_file.exists():
                    logger.info(f"  瑕嗙洊宸插瓨鍦ㄧ殑鏂囦欢: {relative_dir / output_filename}")
                
                # 濡傛灉鏄疧rders鐩綍涓嬬殑鏂囦欢锛屽厛淇濆瓨涓轰复鏃禖SV杩涜鏁版嵁澶勭悊
                temp_csv = None
                if is_orders_dir:
                    # 鍏堜繚瀛樹负涓存椂CSV鏂囦欢杩涜澶勭悊
                    temp_csv = target_subdir / f"{output_file.stem}_temp.csv"
                    df.to_csv(temp_csv, index=False, encoding='utf-8-sig')
                    df_csv = pd.read_csv(temp_csv, encoding='utf-8-sig')
                else:
                    # 闈濷rders鐩綍锛岀洿鎺ヤ繚瀛樹负CSV
                    df.to_csv(output_file, index=False, encoding='utf-8-sig')
                    logger.info(f"  鉁?宸茶浆鎹? {relative_dir / output_filename} ({len(df)} 琛? {len(df.columns)} 鍒?")
                    converted_files.append(output_file)
                    continue  # 璺宠繃鍚庣画澶勭悊
                
                # 濡傛灉鏄疧rders鐩綍涓嬬殑鏂囦欢锛岃繘琛岀壒娈婂鐞?
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
                            # 浠庡晢鍝丼KU涓彁鍙朣KU鍓嶇紑锛堝墠涓や釜閮ㄥ垎锛屼緥濡傦細YOG-001-Bleu-L -> YOG-001锛?
                            def extract_sku_prefix(sku_value):
                                """浠嶴KU鍊间腑鎻愬彇鍓嶇紑锛堝墠涓や釜閮ㄥ垎锛岀敤杩炲瓧绗﹁繛鎺ワ級"""
                                if pd.isna(sku_value) or sku_value == '':
                                    return ''
                                sku_str = str(sku_value).strip()
                                # 鎸夎繛瀛楃鍒嗗壊
                                parts = sku_str.split('-')
                                if len(parts) >= 2:
                                    # 濡傛灉鏈夎嚦灏戜袱涓儴鍒嗭紝杩斿洖鍓嶄袱涓儴鍒嗙敤杩炲瓧绗﹁繛鎺?
                                    return f"{parts[0]}-{parts[1]}"
                                elif len(parts) == 1:
                                    # 濡傛灉鍙湁涓€涓儴鍒嗭紝杩斿洖鍘熷€?
                                    return parts[0]
                                # 濡傛灉娌℃湁閮ㄥ垎锛岃繑鍥炵┖瀛楃涓?
                                return ''
                            
                            # 娣诲姞SKU_Quotation鍒楋紙濡傛灉宸插瓨鍦ㄥ垯瑕嗙洊锛?
                            df_csv['SKU_Quotation'] = df_csv[sku_column].apply(extract_sku_prefix)
                            
                            # 閲嶆柊鎺掑垪鍒楅『搴忥細灏嗗晢鍝丼KU鍜孲KU_Quotation閮芥斁鍦⊿KU鍒楃殑鍙宠竟
                            # 棣栧厛鏌ユ壘SKU鍒楋紙涓嶅寘鍚?鍟嗗搧"鐨凷KU鍒楋級
                            base_sku_column = None
                            for col in df_csv.columns:
                                col_str = str(col).strip()
                                if "SKU" in col_str.upper() and "鍟嗗搧" not in col_str and "QUOTATION" not in col_str.upper():
                                    base_sku_column = col
                                    break
                            
                            # 濡傛灉鎵句笉鍒板熀纭€SKU鍒楋紝浣跨敤鍟嗗搧SKU鍒椾綔涓哄弬鑰?
                            if base_sku_column is None:
                                base_sku_column = sku_column
                            
                            cols = list(df_csv.columns)
                            
                            # 绉婚櫎闇€瑕侀噸鏂版帓搴忕殑鍒?
                            cols_to_reorder = []
                            if sku_column in cols and sku_column != base_sku_column:
                                cols_to_reorder.append(sku_column)
                                cols.remove(sku_column)
                            if 'SKU_Quotation' in cols:
                                cols_to_reorder.append('SKU_Quotation')
                                cols.remove('SKU_Quotation')
                            
                            # 鎵惧埌鍩虹SKU鍒楃殑浣嶇疆
                            if base_sku_column in cols:
                                sku_idx = cols.index(base_sku_column)
                                # 鍦⊿KU鍒楀悗闈緷娆℃彃鍏ュ晢鍝丼KU鍜孲KU_Quotation
                                insert_idx = sku_idx + 1
                                for col_to_insert in cols_to_reorder:
                                    cols.insert(insert_idx, col_to_insert)
                                    insert_idx += 1
                                df_csv = df_csv[cols]
                            
                            logger.info(f"  宸叉坊鍔燬KU_Quotation鍒楋紙浠?{sku_column}'鍒楁彁鍙栵紝浣嶄簬SKU鍒楀彸渚э級")
                            
                            # 娣诲姞QTY_Merged鍒?
                            # 鏌ユ壘Order#鍒楀拰QTY鍒?
                            order_column = None
                            qty_column = None
                            
                            for col in df_csv.columns:
                                col_str = str(col).strip()
                                if "Order#" in col_str or ("Order" in col_str and "#" in col_str):
                                    order_column = col
                                if "QTY" in col_str.upper() and "MERGED" not in col_str.upper():
                                    qty_column = col
                            
                            if order_column is not None and qty_column is not None:
                                # 纭繚QTY鍒椾负鏁板€肩被鍨?
                                df_csv[qty_column] = pd.to_numeric(df_csv[qty_column], errors='coerce').fillna(0)
                                
                                # 鎸塐rder#鍜孲KU_Quotation鍒嗙粍锛岃绠桻TY鎬诲拰
                                # 鍒涘缓鍒嗙粍閿?
                                df_csv['_group_key'] = df_csv[order_column].astype(str) + '_' + df_csv['SKU_Quotation'].astype(str)
                                
                                # 璁＄畻姣忕粍鐨凲TY鎬诲拰
                                qty_sum = df_csv.groupby('_group_key')[qty_column].transform('sum')
                                
                                # 鍒濆鍖朡TY_Merged鍒椾负绌哄瓧绗︿覆
                                df_csv['QTY_Merged'] = ''
                                
                                # 瀵逛簬姣忎釜鍒嗙粍锛屽湪绗竴琛屾樉绀烘€诲拰锛屽叾浠栬鏄剧ず绌?
                                for group_key in df_csv['_group_key'].unique():
                                    group_mask = df_csv['_group_key'] == group_key
                                    group_indices = df_csv[group_mask].index.tolist()
                                    
                                    if len(group_indices) > 0:
                                        # 绗竴琛屾樉绀烘€诲拰锛堣浆鎹负鏁存暟锛岄伩鍏嶅皬鏁帮級
                                        first_idx = group_indices[0]
                                        sum_value = int(qty_sum.loc[first_idx]) if not pd.isna(qty_sum.loc[first_idx]) else ''
                                        df_csv.loc[first_idx, 'QTY_Merged'] = sum_value
                                        # 鍏朵粬琛屼繚鎸佷负绌猴紙宸茬粡鏄┖瀛楃涓诧級
                                
                                # 鍒犻櫎涓存椂鍒?
                                df_csv = df_csv.drop(columns=['_group_key'])
                                
                                # 閲嶆柊鎺掑垪鍒楅『搴忥細灏哘TY_Merged鏀惧湪QTY鍒楃殑鍙宠竟
                                cols = list(df_csv.columns)
                                if 'QTY_Merged' in cols:
                                    cols.remove('QTY_Merged')
                                    # 鎵惧埌QTY鍒楃殑浣嶇疆
                                    qty_idx = cols.index(qty_column) if qty_column in cols else len(cols)
                                    # 鍦≦TY鍒楀悗闈㈡彃鍏TY_Merged
                                    cols.insert(qty_idx + 1, 'QTY_Merged')
                                    df_csv = df_csv[cols]
                                
                                logger.info(f"  宸叉坊鍔燪TY_Merged鍒楋紙鎸塐rder#鍜孲KU_Quotation鍒嗙粍绱姞QTY锛屼綅浜嶲TY鍒楀彸渚э級")
                                
                                # 娣诲姞cost鍜寀psell鍒?
                                # 鏌ユ壘Country鍒?
                                country_column = None
                                for col in df_csv.columns:
                                    col_str = str(col).strip()
                                    if "Country" in col_str or "鍥藉" in col_str:
                                        country_column = col
                                        break
                                
                                if country_column is not None and order_column is not None:
                                    # 鍒濆鍖朿ost鍜寀psell鍒?
                                    df_csv['cost'] = ''
                                    df_csv['upsell'] = ''
                                    
                                    # 鎶ヤ环琛ㄦ枃浠惰矾寰?
                                    quotation_dir = TARGET_DIR / "LG-Le" / "Quotation"
                                    
                                    # 鍔犺浇鎶ヤ环琛ㄦ暟鎹紙鍙姞杞戒竴娆★級
                                    quotation_df = None
                                    upsell_quotation_df = None
                                    
                                    logger.info(f"  鏌ユ壘鎶ヤ环琛ㄦ枃浠讹紝鐩綍: {quotation_dir}")
                                    logger.info(f"  鐩綍鏄惁瀛樺湪: {quotation_dir.exists()}")
                                    
                                    if quotation_dir.exists():
                                        # 鍒楀嚭鎵€鏈塁SV鏂囦欢鐢ㄤ簬璋冭瘯
                                        all_csv_files = list(quotation_dir.glob("*.csv"))
                                        logger.info(f"  鐩綍涓嬫墍鏈塁SV鏂囦欢: {[f.name for f in all_csv_files]}")
                                        
                                        # 鏌ユ壘鎶ヤ环琛ㄦ枃浠讹紙鍖呭惈"鎶ヤ环琛?浣嗕笉鍖呭惈"upsell"锛?
                                        quotation_files = list(quotation_dir.glob("*鎶ヤ环琛?.csv"))
                                        logger.info(f"  鍖归厤'*鎶ヤ环琛?.csv'鐨勬枃浠? {[f.name for f in quotation_files]}")
                                        quotation_files = [f for f in quotation_files if "upsell" not in f.name.lower()]
                                        logger.info(f"  杩囨护鍚庯紙涓嶅惈upsell锛夌殑鎶ヤ环琛ㄦ枃浠? {[f.name for f in quotation_files]}")
                                        
                                        if quotation_files:
                                            try:
                                                quotation_df = pd.read_csv(quotation_files[0], encoding='utf-8-sig')
                                                logger.info(f"  宸插姞杞芥姤浠疯〃鏂囦欢: {quotation_files[0].name} (鍏眥len(quotation_df)}琛?")
                                            except Exception as e:
                                                logger.warning(f"  璇诲彇鎶ヤ环琛ㄦ枃浠跺け璐? {str(e)}")
                                        else:
                                            logger.warning(f"  鏈壘鍒版姤浠疯〃鏂囦欢锛堝寘鍚?鎶ヤ环琛?浣嗕笉鍖呭惈'upsell'锛?)
                                        
                                        # 鏌ユ壘upsell鎶ヤ环琛ㄦ枃浠讹紙鍖呭惈"upsell"鍜?鎶ヤ环琛?锛?
                                        upsell_files = list(quotation_dir.glob("*upsell*鎶ヤ环琛?.csv"))
                                        upsell_files.extend(list(quotation_dir.glob("*鎶ヤ环琛?upsell*.csv")))
                                        logger.info(f"  鍖归厤upsell鎶ヤ环琛ㄦ枃浠? {[f.name for f in upsell_files]}")
                                        
                                        if upsell_files:
                                            try:
                                                upsell_quotation_df = pd.read_csv(upsell_files[0], encoding='utf-8-sig')
                                                logger.info(f"  宸插姞杞絬psell鎶ヤ环琛ㄦ枃浠? {upsell_files[0].name} (鍏眥len(upsell_quotation_df)}琛?")
                                            except Exception as e:
                                                logger.warning(f"  璇诲彇upsell鎶ヤ环琛ㄦ枃浠跺け璐? {str(e)}")
                                        else:
                                            logger.warning(f"  鏈壘鍒皍psell鎶ヤ环琛ㄦ枃浠讹紙鍖呭惈'upsell'鍜?鎶ヤ环琛?锛?)
                                    else:
                                        logger.warning(f"  鎶ヤ环琛ㄧ洰褰曚笉瀛樺湪: {quotation_dir}")
                                    
                                    # 鎸塐rder#鍒嗙粍澶勭悊
                                    for order_num in df_csv[order_column].unique():
                                        order_mask = df_csv[order_column] == order_num
                                        order_rows = df_csv[order_mask].copy()
                                        
                                        # 鎵惧埌璇rder#涓墍鏈夋湁QTY_Merged鍊肩殑琛?
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
                                            # 濡傛灉QTY_Merged娌℃湁鍊硷紝cost鍜寀psell閮戒负绌?
                                            continue
                                        
                                        # 鎵惧埌QTY_Merged鏈€澶х殑琛岋紙璁剧疆cost锛?
                                        max_qty_row = max(rows_with_qty, key=lambda x: x[1])
                                        max_idx = max_qty_row[0]
                                        max_qty_merged = max_qty_row[1]
                                        
                                        # 鑾峰彇鏈€澶TY_Merged琛岀殑淇℃伅
                                        sku_quotation_max = df_csv.loc[max_idx, 'SKU_Quotation']
                                        country_max = df_csv.loc[max_idx, country_column]
                                        
                                        if pd.isna(sku_quotation_max) or str(sku_quotation_max).strip() == '':
                                            continue
                                        if pd.isna(country_max) or str(country_max).strip() == '':
                                            continue
                                        
                                        country_code = str(country_max).strip().upper()
                                        
                                        # 涓烘渶澶TY_Merged鐨勮璁剧疆cost鍊?
                                        if quotation_df is not None:
                                            # 鏌ユ壘SKU_Quotation鍖归厤鐨勮
                                            sku_cols = [col for col in quotation_df.columns if 'SKU' in str(col).upper()]
                                            match_rows = pd.DataFrame()
                                            
                                            for sku_col in sku_cols:
                                                matches = quotation_df[quotation_df[sku_col].astype(str).str.strip() == str(sku_quotation_max).strip()]
                                                if len(matches) > 0:
                                                    match_rows = matches
                                                    break
                                            
                                            if len(match_rows) > 0:
                                                # 鏌ユ壘QTY鍖归厤鐨勮
                                                qty_cols = [col for col in match_rows.columns if 'QTY' in str(col).upper() and 'MERGED' not in str(col).upper()]
                                                found_cost = False
                                                
                                                for _, row in match_rows.iterrows():
                                                    # 妫€鏌TY鏄惁鍖归厤
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
                                                        # 鏌ユ壘鍖呭惈Country浠ｇ爜鐨勫垪
                                                        country_cols = [col for col in match_rows.columns if country_code in str(col).upper()]
                                                        if country_cols:
                                                            cost_value = row[country_cols[0]]
                                                            if pd.notna(cost_value):
                                                                try:
                                                                    # 瀛樺偍鏁板€硷紙Excel鎵撳紑鏃朵細鑷姩搴旂敤鏍煎紡锛?
                                                                    cost_num = float(cost_value)
                                                                    df_csv.loc[max_idx, 'cost'] = cost_num
                                                                except:
                                                                    df_csv.loc[max_idx, 'cost'] = cost_value
                                                                found_cost = True
                                                                break
                                                
                                                if not found_cost:
                                                    df_csv.loc[max_idx, 'cost'] = f'鏈壘鍒癚TY={max_qty_merged}鎴朇ountry={country_code}鐨勬暟鎹?
                                            else:
                                                df_csv.loc[max_idx, 'cost'] = f'鏈壘鍒癝KU_Quotation={sku_quotation_max}鐨勬暟鎹?
                                        else:
                                            df_csv.loc[max_idx, 'cost'] = '鎶ヤ环琛ㄦ枃浠朵笉瀛樺湪'
                                        
                                        # 涓哄叾浠栨湁QTY_Merged鍊肩殑琛岃缃畊psell鍊?
                                        other_rows = [r for r in rows_with_qty if r[0] != max_idx]
                                        
                                        for other_idx, other_qty in other_rows:
                                            other_sku_quotation = df_csv.loc[other_idx, 'SKU_Quotation']
                                            other_country = df_csv.loc[other_idx, country_column]
                                            
                                            if pd.isna(other_sku_quotation) or str(other_sku_quotation).strip() == '':
                                                continue
                                            if pd.isna(other_country) or str(other_country).strip() == '':
                                                continue
                                            
                                            other_country_code = str(other_country).strip().upper()
                                            
                                            # 璁＄畻upsell鍊?
                                            if upsell_quotation_df is not None:
                                                # 鏌ユ壘SKU_Quotation鍖归厤鐨勮
                                                sku_cols = [col for col in upsell_quotation_df.columns if 'SKU' in str(col).upper()]
                                                match_rows = pd.DataFrame()
                                                
                                                for sku_col in sku_cols:
                                                    matches = upsell_quotation_df[upsell_quotation_df[sku_col].astype(str).str.strip() == str(other_sku_quotation).strip()]
                                                    if len(matches) > 0:
                                                        match_rows = matches
                                                        break
                                                
                                                if len(match_rows) > 0:
                                                    # 鏌ユ壘QTY鍖归厤鐨勮
                                                    qty_cols = [col for col in match_rows.columns if 'QTY' in str(col).upper() and 'MERGED' not in str(col).upper()]
                                                    found_upsell = False
                                                    
                                                    for _, row in match_rows.iterrows():
                                                        # 妫€鏌TY鏄惁鍖归厤
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
                                                            # 鏌ユ壘鍖呭惈Country浠ｇ爜鐨勫垪
                                                            country_cols = [col for col in match_rows.columns if other_country_code in str(col).upper()]
                                                            if country_cols:
                                                                upsell_value = row[country_cols[0]]
                                                                if pd.notna(upsell_value):
                                                                    try:
                                                                        # 瀛樺偍鏁板€硷紙Excel鎵撳紑鏃朵細鑷姩搴旂敤鏍煎紡锛?
                                                                        upsell_num = float(upsell_value)
                                                                        df_csv.loc[other_idx, 'upsell'] = upsell_num
                                                                    except:
                                                                        df_csv.loc[other_idx, 'upsell'] = upsell_value
                                                                    found_upsell = True
                                                                    break
                                                
                                                if not found_upsell:
                                                    df_csv.loc[other_idx, 'upsell'] = f'鏈壘鍒癚TY={other_qty}鎴朇ountry={other_country_code}鐨勬暟鎹?
                                            else:
                                                df_csv.loc[other_idx, 'upsell'] = 'upsell鎶ヤ环琛ㄦ枃浠朵笉瀛樺湪'
                                    
                                    # 閲嶆柊鎺掑垪鍒楅『搴忥細灏哻ost鍜寀psell鏀惧湪Country鍒楃殑宸﹁竟
                                    cols = list(df_csv.columns)
                                    if 'cost' in cols and 'upsell' in cols and country_column in cols:
                                        cols.remove('cost')
                                        cols.remove('upsell')
                                        country_idx = cols.index(country_column)
                                        cols.insert(country_idx, 'upsell')
                                        cols.insert(country_idx, 'cost')
                                        df_csv = df_csv[cols]
                                    
                                    # 鍦ㄦ渶鍚庝竴琛屾坊鍔爏um琛岋紙涓嶅垱寤簊um鍒楋紝鍙湪鏈€鍚庝竴琛屾樉绀簊um鏂囨湰鍜屽€硷級
                                    # 鎵惧埌cost鍒楀拰upsell鍒楃殑浣嶇疆锛堝湪閲嶆柊鎺掑垪鍒楅『搴忎箣鍚庯級
                                    cost_idx = list(df_csv.columns).index('cost') if 'cost' in df_csv.columns else -1
                                    upsell_idx = list(df_csv.columns).index('upsell') if 'upsell' in df_csv.columns else -1
                                    
                                    # 鎵惧埌cost鍒楀乏杈圭殑鍒楋紙鐢ㄤ簬鏄剧ず"sum"鏂囨湰锛?
                                    sum_text_col_idx = -1
                                    if cost_idx >= 0:
                                        # 濡傛灉cost鍒椾笉鏄涓€鍒楋紝浣跨敤cost鍒楀乏杈圭殑鍒?
                                        if cost_idx > 0:
                                            sum_text_col_idx = cost_idx - 1
                                        else:
                                            # 濡傛灉cost鍒楁槸绗竴鍒楋紝浣跨敤绗竴鍒?
                                            sum_text_col_idx = 0
                                    
                                    # 鍒涘缓鏂拌锛堟渶鍚庝竴琛岋級
                                    new_row = pd.Series(index=df_csv.columns, dtype=object)
                                    new_row_index = len(df_csv)
                                    df_csv.loc[new_row_index] = new_row
                                    
                                    # 鍦╟ost鍒楀乏杈圭殑鍒楁樉绀?sum"鏂囨湰锛堜笉鍒涘缓鏂板垪锛岀洿鎺ヨ缃€硷級
                                    if sum_text_col_idx >= 0:
                                        sum_text_col_name = df_csv.columns[sum_text_col_idx]
                                        df_csv.loc[new_row_index, sum_text_col_name] = 'sum'
                                    
                                    # 璁剧疆cost鍒楃殑鍊硷紙浣跨敤鍏紡姹傚拰锛?
                                    if 'cost' in df_csv.columns and 'upsell' in df_csv.columns and cost_idx >= 0 and upsell_idx >= 0:
                                        # 璁＄畻cost鍜寀psell鍒楃殑鏁版嵁鑼冨洿
                                        # Excel琛屽彿浠?寮€濮嬶紝绗?琛屾槸鏍囬锛屾暟鎹粠绗?琛屽紑濮?
                                        data_start_row = 2
                                        data_end_row = new_row_index + 1  # 鏈€鍚庝竴琛屾暟鎹紙涓嶅寘鎷瑂um琛岋級
                                        
                                        # 灏嗗垪绱㈠紩杞崲涓篍xcel鍒楀瓧姣嶏紙A, B, C, ...锛?
                                        def col_index_to_letter(n):
                                            """灏嗗垪绱㈠紩杞崲涓篍xcel鍒楀瓧姣嶏紙0=A, 1=B, ...锛?""
                                            result = ""
                                            while n >= 0:
                                                result = chr(65 + (n % 26)) + result
                                                n = n // 26 - 1
                                            return result
                                        
                                        cost_col_letter = col_index_to_letter(cost_idx)
                                        upsell_col_letter = col_index_to_letter(upsell_idx)
                                        
                                        # 鐢熸垚鍏紡锛?SUM(cost鍒楄寖鍥?+SUM(upsell鍒楄寖鍥?
                                        # 渚嬪锛?SUM(C2:C10)+SUM(D2:D10)
                                        cost_formula = f"=SUM({cost_col_letter}{data_start_row}:{cost_col_letter}{data_end_row})+SUM({upsell_col_letter}{data_start_row}:{upsell_col_letter}{data_end_row})"
                                        df_csv.loc[new_row_index, 'cost'] = cost_formula
                                        
                                        logger.info(f"  宸叉坊鍔爏um琛岋紝鍏紡: {cost_formula}")
                                    
                                    logger.info(f"  宸叉坊鍔燾ost鍜寀psell鍒楋紙浣嶄簬Country鍒楀乏渚э級锛屽苟鍦ㄦ渶鍚庝竴琛屾坊鍔爏um琛岋紙浣跨敤鍏紡璁＄畻锛?)
                                else:
                                    if country_column is None:
                                        logger.warning(f"  鏈壘鍒癈ountry鍒楋紝璺宠繃娣诲姞cost鍜寀psell鍒?)
                            else:
                                missing_cols = []
                                if order_column is None:
                                    missing_cols.append("Order#")
                                if qty_column is None:
                                    missing_cols.append("QTY")
                                logger.warning(f"  鏈壘鍒皗', '.join(missing_cols)}鍒楋紝璺宠繃娣诲姞QTY_Merged鍒?)
                            
                            # 淇濆瓨鏂囦欢锛圤rders鐩綍淇濆瓨涓篍xcel鏍煎紡锛屽叾浠栦繚瀛樹负CSV锛?
                            if is_orders_dir:
                                # 淇濆瓨涓篍xcel鏍煎紡浠ユ敮鎸佽揣甯佹牸寮忓拰鍏紡
                                from openpyxl import Workbook
                                from openpyxl.styles import Font, PatternFill
                                from openpyxl.utils import get_column_letter
                                from openpyxl.utils.dataframe import dataframe_to_rows
                                
                                wb = Workbook()
                                ws = wb.active
                                ws.title = safe_sheet_name if safe_sheet_name else "Sheet1"
                                
                                # 鍐荤粨琛ㄥご锛堢涓€琛岋級
                                ws.freeze_panes = 'A2'
                                
                                # 鍐欏叆鏁版嵁
                                cost_col_idx = None
                                upsell_col_idx = None
                                
                                for r_idx, row in enumerate(dataframe_to_rows(df_csv, index=False, header=True), 1):
                                    for c_idx, value in enumerate(row, 1):
                                        if c_idx > len(df_csv.columns):
                                            break
                                        col_name = df_csv.columns[c_idx - 1]
                                        cell = ws.cell(row=r_idx, column=c_idx, value=value)
                                        
                                        # 璁板綍cost鍜寀psell鍒楃殑浣嶇疆
                                        if r_idx == 1:  # 鏍囬琛?
                                            if col_name == 'cost':
                                                cost_col_idx = c_idx
                                            elif col_name == 'upsell':
                                                upsell_col_idx = c_idx
                                        
                                        # 濡傛灉鏄痗ost鎴杣psell鍒楋紝璁剧疆璐у竵鏍煎紡锛圲S$锛屼袱浣嶅皬鏁帮級
                                        if col_name in ['cost', 'upsell']:
                                            # 璁剧疆璐у竵鏍煎紡涓篣S$锛屼繚鐣欎袱浣嶅皬鏁帮紙鎵€鏈夊崟鍏冩牸锛屽寘鎷叕寮忓拰鏁板€硷級
                                            cell.number_format = '$#,##0.00'
                                            
                                            # 濡傛灉鏄敊璇俊鎭紙鍖呭惈"鏈壘鍒?绛夛級锛岃缃负绾㈣壊鑳屾櫙
                                            if isinstance(value, str) and ('鏈壘鍒? in value or '涓嶅瓨鍦? in value):
                                                cell.fill = PatternFill(start_color="FF0000", end_color="FF0000", fill_type="solid")
                                                cell.font = Font(color="FFFFFF")  # 鐧借壊鏂囧瓧
                                        
                                        # 濡傛灉鏄叾浠栧垪鐨勯敊璇俊鎭紝涔熻缃负绾㈣壊
                                        elif isinstance(value, str) and ('鏈壘鍒? in value or '涓嶅瓨鍦? in value):
                                            cell.fill = PatternFill(start_color="FF0000", end_color="FF0000", fill_type="solid")
                                            cell.font = Font(color="FFFFFF")  # 鐧借壊鏂囧瓧
                                
                                # 涓烘暣涓猚ost鍜寀psell鍒楄缃揣甯佹牸寮忥紙纭繚鎵€鏈夊崟鍏冩牸閮芥湁鏍煎紡锛屽寘鎷┖鍗曞厓鏍硷級
                                if cost_col_idx:
                                    for row in ws.iter_rows(min_row=2, max_row=ws.max_row, min_col=cost_col_idx, max_col=cost_col_idx):
                                        for cell in row:
                                            # 涓烘墍鏈夊崟鍏冩牸璁剧疆鏍煎紡锛堝寘鎷┖鍗曞厓鏍笺€佹暟鍊笺€佸叕寮忥級
                                            cell.number_format = '$#,##0.00'
                                
                                if upsell_col_idx:
                                    for row in ws.iter_rows(min_row=2, max_row=ws.max_row, min_col=upsell_col_idx, max_col=upsell_col_idx):
                                        for cell in row:
                                            # 涓烘墍鏈夊崟鍏冩牸璁剧疆鏍煎紡锛堝寘鎷┖鍗曞厓鏍笺€佹暟鍊笺€佸叕寮忥級
                                            cell.number_format = '$#,##0.00'
                                
                                # 楂樹寒鏄剧ず鏈€鍚庝竴琛岋紙sum琛岋級
                                if ws.max_row > 1:
                                    last_row = ws.max_row
                                    # 鎵惧埌鏄剧ず"sum"鏂囨湰鐨勫垪鍜宑ost鍒?
                                    sum_text_col_idx = None
                                    for c_idx in range(1, ws.max_column + 1):
                                        cell = ws.cell(row=last_row, column=c_idx)
                                        if cell.value == 'sum':
                                            sum_text_col_idx = c_idx
                                            break
                                    
                                    # 楂樹寒鏄剧ず鏈€鍚庝竴琛?
                                    from openpyxl.styles import Alignment
                                    highlight_fill = PatternFill(start_color="FFFF00", end_color="FFFF00", fill_type="solid")  # 榛勮壊鑳屾櫙
                                    highlight_font = Font(bold=True)  # 绮椾綋
                                    
                                    # 楂樹寒鏄剧ずsum鏂囨湰鍗曞厓鏍?
                                    if sum_text_col_idx:
                                        sum_cell = ws.cell(row=last_row, column=sum_text_col_idx)
                                        sum_cell.fill = highlight_fill
                                        sum_cell.font = highlight_font
                                        sum_cell.alignment = Alignment(horizontal='center')
                                    
                                    # 楂樹寒鏄剧ずcost鍒楋紙鏈€鍚庝竴琛岋級
                                    if cost_col_idx:
                                        cost_cell = ws.cell(row=last_row, column=cost_col_idx)
                                        cost_cell.fill = highlight_fill
                                        cost_cell.font = highlight_font
                                
                                # 淇濆瓨Excel鏂囦欢
                                wb.save(output_file)
                                
                                # 鍏抽棴宸ヤ綔绨匡紝閲婃斁鏂囦欢鍙ユ焺
                                wb.close()
                                
                                # 鍒犻櫎涓存椂CSV鏂囦欢
                                if temp_csv.exists():
                                    try:
                                        temp_csv.unlink()
                                    except Exception as e:
                                        logger.warning(f"  鍒犻櫎涓存椂CSV鏂囦欢澶辫触: {str(e)}")
                                
                                logger.info(f"  宸蹭繚瀛樹负Excel鏍煎紡锛堟敮鎸佽揣甯佹牸寮忓拰鍏紡锛?)
                            else:
                                # 淇濆瓨涓篊SV鏍煎紡
                                df_csv.to_csv(output_file, index=False, encoding='utf-8-sig')
                        else:
                            logger.warning(f"  鏈壘鍒板晢鍝丼KU鍒楋紝璺宠繃娣诲姞SKU_Quotation鍒?)
                            # 鍗充娇娌℃湁鍟嗗搧SKU鍒楋紝涔熻淇濆瓨鏂囦欢
                            if is_orders_dir:
                                # 淇濆瓨涓篍xcel鏍煎紡
                                from openpyxl import Workbook
                                from openpyxl.utils.dataframe import dataframe_to_rows
                                
                                wb = Workbook()
                                ws = wb.active
                                ws.title = safe_sheet_name if safe_sheet_name else "Sheet1"
                                
                                # 鍐荤粨琛ㄥご锛堢涓€琛岋級
                                ws.freeze_panes = 'A2'
                                
                                # 鍐欏叆鏁版嵁
                                for r_idx, row in enumerate(dataframe_to_rows(df_csv, index=False, header=True), 1):
                                    ws.append(row)
                                
                                # 璋冩暣鍒楀
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
                                
                                # 鍒犻櫎涓存椂CSV鏂囦欢
                                if temp_csv.exists():
                                    temp_csv.unlink()
                                
                                logger.info(f"  宸蹭繚瀛樹负Excel鏍煎紡锛堟湭娣诲姞SKU_Quotation鍒楋級")
                            else:
                                # 淇濆瓨涓篊SV鏍煎紡
                                df_csv.to_csv(output_file, index=False, encoding='utf-8-sig')
                    except Exception as e:
                        logger.error(f"  澶勭悊SKU_Quotation鍒楁椂鍑洪敊: {str(e)}")
                        # 鍗充娇鍑洪敊锛屼篃瑕佸皾璇曚繚瀛樻枃浠?
                        try:
                            if is_orders_dir:
                                # 淇濆瓨涓篍xcel鏍煎紡
                                from openpyxl import Workbook
                                from openpyxl.utils.dataframe import dataframe_to_rows
                                
                                wb = Workbook()
                                ws = wb.active
                                ws.title = safe_sheet_name if safe_sheet_name else "Sheet1"
                                
                                # 鍐荤粨琛ㄥご锛堢涓€琛岋級
                                ws.freeze_panes = 'A2'
                                
                                # 鍐欏叆鏁版嵁
                                for r_idx, row in enumerate(dataframe_to_rows(df_csv, index=False, header=True), 1):
                                    ws.append(row)
                                
                                # 璋冩暣鍒楀
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
                                
                                # 鍒犻櫎涓存椂CSV鏂囦欢
                                if temp_csv.exists():
                                    temp_csv.unlink()
                                
                                logger.info(f"  宸蹭繚瀛樹负Excel鏍煎紡锛堝鐞嗗嚭閿欙紝浣嗗凡淇濆瓨鍘熷鏁版嵁锛?)
                            else:
                                # 淇濆瓨涓篊SV鏍煎紡
                                df_csv.to_csv(output_file, index=False, encoding='utf-8-sig')
                        except Exception as e2:
                            logger.error(f"  淇濆瓨鏂囦欢鏃跺嚭閿? {str(e2)}")
                
                logger.info(f"  鉁?宸茶浆鎹? {relative_dir / output_filename} ({len(df)} 琛? {len(df.columns)} 鍒?")
                converted_files.append(output_file)
                
            except Exception as e:
                logger.error(f"  杞崲sheet '{sheet_name}' 鏃跺嚭閿? {str(e)}")
                continue
        
        # 濡傛灉杞崲鎴愬姛锛屽垹闄ゅ師鏂囦欢
        if converted_files:
            # 纭繚ExcelFile瀵硅薄宸插叧闂紝閲婃斁鏂囦欢鍙ユ焺
            if 'excel_file' in locals():
                excel_file.close()
            
            # 灏濊瘯鍒犻櫎鍘熸枃浠讹紝濡傛灉澶辫触鍒欓噸璇?
            deleted = False
            max_retries = 5  # 增加重试次数
            retry_delay = 1.0  # 重试延迟时间
            for attempt in range(max_retries):
                try:
                    # 绛夊緟涓€灏忔鏃堕棿锛岀‘淇濇枃浠跺彞鏌勫凡閲婃斁
                    if attempt > 0:
                        time.sleep(0.5)
                    
                    xlsx_path.unlink()  # 鍒犻櫎鍘焫lsx鏂囦欢
                    logger.info(f"鉁?宸插垹闄ゅ師鏂囦欢: {relative_path}")
                    deleted = True
                    break
                except PermissionError as e:
                    if attempt < max_retries - 1:
                        logger.warning(f"鍒犻櫎鍘熸枃浠跺け璐ワ紙鏂囦欢鍙兘琚崰鐢級锛寋0.5}绉掑悗閲嶈瘯 ({attempt + 1}/{max_retries}): {relative_path}")
                    else:
                        logger.error(f"鍒犻櫎鍘熸枃浠跺け璐ワ紙鏂囦欢琚崰鐢紝宸查噸璇晎max_retries}娆★級: {relative_path} - {str(e)}")
                except Exception as e:
                    logger.error(f"鍒犻櫎鍘熸枃浠跺け璐? {relative_path} - {str(e)}")
                    break
        
        # 鏍囪鏂囦欢宸插鐞?
        processed_files.add(file_key)
        
        if deleted:
            logger.info(f"鉁?鏂囦欢杞崲瀹屾垚骞跺凡鍒犻櫎: {relative_path} -> {len(converted_files)} 涓狢SV鏂囦欢")
        else:
            logger.warning(f"鉁?鏂囦欢杞崲瀹屾垚浣嗗垹闄ゅけ璐? {relative_path} -> {len(converted_files)} 涓狢SV鏂囦欢")
        
        return True
    else:
            logger.warning(f"鏂囦欢杞崲瀹屾垚锛屼絾鏈敓鎴愪换浣旵SV: {relative_path}")
            return False
        
    except Exception as e:
        logger.error(f"杞崲鏂囦欢鏃跺嚭閿?{relative_path}: {str(e)}")
        return False


def sync_directory_structure():
    """鍚屾convertedData鐨勭洰褰曠粨鏋勶紝浣垮叾涓巗ourceData淇濇寔涓€鑷?""
    if not SOURCE_DIR.exists():
        return
    
    try:
        # 閬嶅巻sourceData鐨勬墍鏈夌洰褰曪紝鍦╟onvertedData涓垱寤哄搴旂殑鐩綍缁撴瀯
        for source_dir in SOURCE_DIR.rglob("*"):
            if source_dir.is_dir():
                # 璁＄畻鐩稿璺緞
                try:
                    relative_dir = source_dir.relative_to(SOURCE_DIR)
                    target_dir = TARGET_DIR / relative_dir
                    # 鍒涘缓瀵瑰簲鐨勭洰鏍囩洰褰曪紙濡傛灉涓嶅瓨鍦級
                    target_dir.mkdir(parents=True, exist_ok=True)
                except ValueError:
                    continue
    except Exception as e:
        logger.debug(f"鍚屾鐩綍缁撴瀯鏃跺嚭閿? {str(e)}")


def scan_and_convert():
    """閫掑綊鎵弿婧愮洰褰曞強鍏舵墍鏈夊瓙鐩綍锛岃浆鎹㈡墍鏈墄lsx鏂囦欢"""
    if not SOURCE_DIR.exists():
        logger.warning(f"婧愮洰褰曚笉瀛樺湪: {SOURCE_DIR}")
        return
    
    # 鍏堝悓姝ョ洰褰曠粨鏋?
    sync_directory_structure()
    
    # 閫掑綊鏌ユ壘鎵€鏈墄lsx鏂囦欢锛堝寘鎷瓙鐩綍锛?
    xlsx_files = list(SOURCE_DIR.rglob("*.xlsx"))
    xlsx_files.extend(SOURCE_DIR.rglob("*.XLSX"))  # 澶勭悊澶у啓鎵╁睍鍚?
    
    if not xlsx_files:
        logger.debug("鏈彂鐜版柊鐨剎lsx鏂囦欢")
        return
    
    logger.info(f"鍙戠幇 {len(xlsx_files)} 涓獂lsx鏂囦欢")
    
    for xlsx_file in xlsx_files:
        # 璁＄畻鐩稿浜嶴OURCE_DIR鐨勭浉瀵硅矾寰?
        try:
            relative_path = xlsx_file.relative_to(SOURCE_DIR)
            convert_xlsx_to_csv(xlsx_file, relative_path)
        except ValueError:
            # 濡傛灉鏃犳硶璁＄畻鐩稿璺緞锛屼娇鐢ㄦ枃浠跺悕
            convert_xlsx_to_csv(xlsx_file, xlsx_file.name)


def main():
    """涓诲嚱鏁?""
    logger.info("=" * 60)
    logger.info("XLSX Converter Service (xlsx-converter) 鍚姩")
    logger.info("=" * 60)
    
    ensure_directories()
    
    logger.info(f"鐩戞帶闂撮殧: {CHECK_INTERVAL} 绉?)
    logger.info("绋嬪簭杩愯涓紝鎸?Ctrl+C 鍋滄...")
    logger.info("")
    
    try:
        while True:
            scan_and_convert()
            logger.debug(f"绛夊緟 {CHECK_INTERVAL} 绉掑悗杩涜涓嬫妫€鏌?..")
            time.sleep(CHECK_INTERVAL)
            
    except KeyboardInterrupt:
        logger.info("")
        logger.info("绋嬪簭琚敤鎴蜂腑鏂紝姝ｅ湪閫€鍑?..")
    except Exception as e:
        logger.error(f"绋嬪簭杩愯鍑洪敊: {str(e)}", exc_info=True)
    finally:
        logger.info("绋嬪簭宸插仠姝?)


if __name__ == "__main__":
    main()

