# Order Converter 启动脚本（解决中文乱码问题）
# 设置控制台编码为UTF-8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8
chcp 65001 | Out-Null

# 切换到脚本目录
Set-Location $PSScriptRoot

# 运行程序
Write-Host "正在启动 Order Converter..." -ForegroundColor Green
Write-Host "提示: 按 Ctrl+C 停止程序" -ForegroundColor Yellow
Write-Host ""

python xlsx_to_csv_converter.py
