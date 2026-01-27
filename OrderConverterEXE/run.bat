@echo off
chcp 65001 >nul
echo ============================================================
echo XLSX Converter Service - 运行脚本
echo ============================================================
echo.

echo [1/3] 恢复 NuGet 包...
dotnet restore
if %errorlevel% neq 0 (
    echo.
    echo 错误: 无法恢复 NuGet 包
    echo 请检查网络连接，或参考 运行说明.md
    pause
    exit /b 1
)

echo.
echo [2/3] 构建项目...
dotnet build
if %errorlevel% neq 0 (
    echo.
    echo 错误: 构建失败
    pause
    exit /b 1
)

echo.
echo [3/3] 运行程序...
echo 按 Ctrl+C 可以停止程序
echo.
dotnet run

pause
