@echo off
REM Order Converter 启动脚本（解决中文乱码问题）
chcp 65001 >nul 2>&1
cd /d "%~dp0"

echo 正在启动 Order Converter...
echo 提示: 按 Ctrl+C 停止程序
echo.

python xlsx_to_csv_converter.py
