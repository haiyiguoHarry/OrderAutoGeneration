# XLSX Converter Service - PowerShell 运行脚本
$OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "XLSX Converter Service - 运行脚本" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "[1/3] 恢复 NuGet 包..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "错误: 无法恢复 NuGet 包" -ForegroundColor Red
    Write-Host "请检查网络连接，或参考 运行说明.md" -ForegroundColor Red
    Read-Host "按 Enter 键退出"
    exit 1
}

Write-Host ""
Write-Host "[2/3] 构建项目..." -ForegroundColor Yellow
dotnet build
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "错误: 构建失败" -ForegroundColor Red
    Read-Host "按 Enter 键退出"
    exit 1
}

Write-Host ""
Write-Host "[3/3] 运行程序..." -ForegroundColor Yellow
Write-Host "按 Ctrl+C 可以停止程序" -ForegroundColor Green
Write-Host ""
dotnet run

Read-Host "按 Enter 键退出"
