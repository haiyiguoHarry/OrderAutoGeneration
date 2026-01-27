@echo off
REM XLSX转换器管理脚本 (Windows批处理版本)
REM 程序名称: XLSX Converter Service (xlsx-converter)
REM 编码: UTF-8

chcp 65001 >nul 2>&1
setlocal enabledelayedexpansion

if "%1"=="" (
    echo 用法: manage.bat [start^|stop^|restart^|status^|logs]
    echo.
    echo 命令:
    echo   start   - 启动服务
    echo   stop    - 停止服务
    echo   restart - 重启服务
    echo   status  - 查看状态
    echo   logs    - 查看日志
    goto :end
)

cd /d "%~dp0"

set SCRIPT_NAME=xlsx-converter
set SCRIPT_FILE=xlsx_to_csv_converter.py
set LOG_FILE=xlsx_converter.log

if "%1"=="start" goto :start
if "%1"=="stop" goto :stop
if "%1"=="restart" goto :restart
if "%1"=="status" goto :status
if "%1"=="logs" goto :logs

echo 未知命令: %1
goto :end

:start
echo 正在启动 %SCRIPT_NAME%...

REM 检查Python是否安装
python --version >nul 2>&1
if errorlevel 1 (
    echo 错误: 未找到Python，请先安装Python
    goto :end
)

REM 检查脚本文件是否存在
if not exist "%SCRIPT_FILE%" (
    echo 错误: 脚本文件不存在: %SCRIPT_FILE%
    goto :end
)

REM 检查是否已在运行
tasklist /FI "IMAGENAME eq python.exe" 2>nul | find /I "python.exe" >nul
if errorlevel 1 (
    REM 没有Python进程，直接启动
    goto :do_start
)

REM 检查是否有xlsx-converter进程在运行
for /f "tokens=2" %%i in ('tasklist /FI "IMAGENAME eq python.exe" /FO CSV ^| findstr /I "python.exe"') do (
    set PID=%%i
    set PID=!PID:"=!
    wmic process where "ProcessId=!PID!" get CommandLine 2>nul | findstr /I "xlsx_to_csv_converter" >nul
    if !errorlevel!==0 (
        echo %SCRIPT_NAME% 已在运行中 (PID: !PID!)
        goto :end
    )
)

:do_start
start /B python "%SCRIPT_FILE%"
timeout /t 2 /nobreak >nul

REM 验证是否启动成功
timeout /t 1 /nobreak >nul
for /f "tokens=2" %%i in ('tasklist /FI "IMAGENAME eq python.exe" /FO CSV ^| findstr /I "python.exe"') do (
    set PID=%%i
    set PID=!PID:"=!
    wmic process where "ProcessId=!PID!" get CommandLine 2>nul | findstr /I "xlsx_to_csv_converter" >nul
    if !errorlevel!==0 (
        echo %SCRIPT_NAME% 启动成功 (PID: !PID!)
        echo 提示: 使用 'manage.bat logs' 查看日志
        goto :end
    )
)

echo %SCRIPT_NAME% 启动失败，请检查日志文件: %LOG_FILE%
goto :end

:stop
echo 正在停止 %SCRIPT_NAME%...

set FOUND=0
for /f "tokens=2" %%i in ('tasklist /FI "IMAGENAME eq python.exe" /FO CSV 2^>nul ^| findstr /I "python.exe"') do (
    set PID=%%i
    set PID=!PID:"=!
    wmic process where "ProcessId=!PID!" get CommandLine 2>nul | findstr /I "xlsx_to_csv_converter" >nul
    if !errorlevel!==0 (
        taskkill /F /PID !PID! >nul 2>&1
        if !errorlevel!==0 (
            echo 已停止进程 (PID: !PID!)
            set FOUND=1
        )
    )
)

if !FOUND!==0 (
    echo %SCRIPT_NAME% 未运行
) else (
    timeout /t 1 /nobreak >nul
    REM 验证是否全部停止
    set STILL_RUNNING=0
    for /f "tokens=2" %%i in ('tasklist /FI "IMAGENAME eq python.exe" /FO CSV 2^>nul ^| findstr /I "python.exe"') do (
        set PID=%%i
        set PID=!PID:"=!
        wmic process where "ProcessId=!PID!" get CommandLine 2>nul | findstr /I "xlsx_to_csv_converter" >nul
        if !errorlevel!==0 (
            set STILL_RUNNING=1
        )
    )
    if !STILL_RUNNING!==0 (
        echo %SCRIPT_NAME% 已成功停止
    ) else (
        echo 警告: 部分进程可能仍在运行
    )
)
goto :end

:restart
call manage.bat stop
timeout /t 2 /nobreak >nul
call manage.bat start
goto :end

:status
echo.
echo === %SCRIPT_NAME% 状态 ===
echo.

REM 检查Python
python --version >nul 2>&1
if errorlevel 1 (
    echo Python: 未安装
) else (
    for /f "delims=" %%v in ('python --version 2^>^&1') do echo Python: %%v
)

echo.

REM 检查进程状态
set RUNNING=0
for /f "tokens=2" %%i in ('tasklist /FI "IMAGENAME eq python.exe" /FO CSV 2^>nul ^| findstr /I "python.exe"') do (
    set PID=%%i
    set PID=!PID:"=!
    wmic process where "ProcessId=!PID!" get CommandLine 2>nul | findstr /I "xlsx_to_csv_converter" >nul
    if !errorlevel!==0 (
        if !RUNNING!==0 (
            echo 状态: 运行中
            echo   PID: !PID!
            set RUNNING=1
        ) else (
            echo   PID: !PID!
        )
    )
)

if !RUNNING!==0 (
    echo 状态: 未运行
)

REM 检查日志文件
echo.
if exist "%LOG_FILE%" (
    echo 日志文件: %LOG_FILE%
    for %%A in ("%LOG_FILE%") do (
        set SIZE=%%~zA
        set /a SIZE_KB=!SIZE!/1024
        echo   大小: !SIZE_KB! KB
    )
    for %%A in ("%LOG_FILE%") do echo   最后修改: %%~tA
) else (
    echo 日志文件: 不存在
)

REM 检查脚本文件
echo.
if exist "%SCRIPT_FILE%" (
    echo 脚本文件: %SCRIPT_FILE%
    for %%A in ("%SCRIPT_FILE%") do echo   最后修改: %%~tA
) else (
    echo 脚本文件: 不存在
)

echo.
goto :end

:logs
if not exist "%LOG_FILE%" (
    echo 日志文件不存在: %LOG_FILE%
    goto :end
)

echo.
echo === %SCRIPT_NAME% 日志 (最后30行) ===
echo.

REM 使用PowerShell显示最后30行（支持UTF-8）
powershell -Command "Get-Content '%LOG_FILE%' -Tail 30 -Encoding UTF8" 2>nul
if errorlevel 1 (
    REM 如果PowerShell失败，使用type命令（可能显示乱码）
    echo 提示: 建议使用 'manage.ps1 logs' 查看日志以获得更好的UTF-8支持
    echo.
    for /f "skip=^<skip_lines^>" %%a in ('find /c /v "" ^< "%LOG_FILE%"') do set TOTAL=%%a
    set /a SKIP=!TOTAL!-30
    if !SKIP! lss 0 set SKIP=0
    more +!SKIP! "%LOG_FILE%"
)

echo.
echo 提示: 使用 'manage.ps1 logs' 查看更多日志
echo.
goto :end

:end
endlocal
