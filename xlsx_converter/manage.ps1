# XLSX转换器管理脚本
# 程序名称: XLSX Converter Service (xlsx-converter)
# 编码: UTF-8

param(
    [Parameter(Position=0)]
    [ValidateSet("start", "stop", "restart", "status", "logs", "install", "uninstall")]
    [string]$Action = "status"
)

$ScriptName = "xlsx-converter"
$ScriptPath = Join-Path $PSScriptRoot "xlsx_to_csv_converter.py"
$LogFile = Join-Path $PSScriptRoot "xlsx_converter.log"
$PythonExe = "python"

# 检查Python是否安装
function Test-PythonInstalled {
    try {
        $version = & $PythonExe --version 2>&1
        if ($LASTEXITCODE -eq 0) {
            return $true
        }
    } catch {
        return $false
    }
    return $false
}

# 获取运行中的xlsx-converter进程
function Get-XlsxConverterProcess {
    $processes = @()
    
    try {
        $allPython = Get-Process python -ErrorAction SilentlyContinue
        
        foreach ($proc in $allPython) {
            try {
                $wmi = Get-WmiObject Win32_Process -Filter "ProcessId = $($proc.Id)" -ErrorAction SilentlyContinue
                if ($wmi -and $wmi.CommandLine -like "*xlsx_to_csv_converter*") {
                    $processes += $proc
                }
            } catch {
                # 忽略无法访问的进程
            }
        }
    } catch {
        # 如果没有python进程，返回空数组
    }
    
    return $processes
}

# 启动服务
function Start-XlsxConverter {
    Write-Host "正在启动 $ScriptName..." -ForegroundColor Green
    
    # 检查Python
    if (-not (Test-PythonInstalled)) {
        Write-Host "错误: 未找到Python，请先安装Python" -ForegroundColor Red
        return
    }
    
    # 检查脚本文件
    if (-not (Test-Path $ScriptPath)) {
        Write-Host "错误: 脚本文件不存在: $ScriptPath" -ForegroundColor Red
        return
    }
    
    # 检查是否已在运行
    $existing = Get-XlsxConverterProcess
    if ($existing) {
        Write-Host "$ScriptName 已在运行中 (PID: $($existing[0].Id))" -ForegroundColor Yellow
        return
    }
    
    # 启动进程
    try {
        $process = Start-Process -FilePath $PythonExe -ArgumentList "`"$ScriptPath`"" -WindowStyle Hidden -PassThru
        Start-Sleep -Seconds 2
        
        # 验证是否启动成功
        $processes = Get-XlsxConverterProcess
        if ($processes) {
            Write-Host "$ScriptName 启动成功 (PID: $($processes[0].Id))" -ForegroundColor Green
            Write-Host "提示: 使用 'manage.ps1 logs' 查看日志" -ForegroundColor Cyan
        } else {
            Write-Host "$ScriptName 启动失败，请检查日志文件: $LogFile" -ForegroundColor Red
        }
    } catch {
        Write-Host "启动失败: $_" -ForegroundColor Red
    }
}

# 停止服务
function Stop-XlsxConverter {
    Write-Host "正在停止 $ScriptName..." -ForegroundColor Yellow
    
    $processes = Get-XlsxConverterProcess
    if (-not $processes) {
        Write-Host "$ScriptName 未运行" -ForegroundColor Yellow
        return
    }
    
    $stoppedCount = 0
    foreach ($proc in $processes) {
        try {
            Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
            Write-Host "已停止进程 (PID: $($proc.Id))" -ForegroundColor Green
            $stoppedCount++
        } catch {
            Write-Host "停止进程失败 (PID: $($proc.Id)): $_" -ForegroundColor Red
        }
    }
    
    Start-Sleep -Seconds 1
    
    # 验证是否全部停止
    $remaining = Get-XlsxConverterProcess
    if (-not $remaining) {
        Write-Host "$ScriptName 已成功停止 (共停止 $stoppedCount 个进程)" -ForegroundColor Green
    } else {
        Write-Host "警告: 仍有 $($remaining.Count) 个进程在运行" -ForegroundColor Red
    }
}

# 重启服务
function Restart-XlsxConverter {
    Write-Host "正在重启 $ScriptName..." -ForegroundColor Cyan
    Stop-XlsxConverter
    Start-Sleep -Seconds 2
    Start-XlsxConverter
}

# 显示状态
function Show-XlsxConverterStatus {
    Write-Host ""
    Write-Host "=== $ScriptName 状态 ===" -ForegroundColor Cyan
    Write-Host ""
    
    # 检查Python
    if (Test-PythonInstalled) {
        $version = & $PythonExe --version 2>&1
        Write-Host "Python: $version" -ForegroundColor White
    } else {
        Write-Host "Python: 未安装" -ForegroundColor Red
    }
    
    Write-Host ""
    
    # 检查进程状态
    $processes = Get-XlsxConverterProcess
    if ($processes) {
        Write-Host "状态: 运行中" -ForegroundColor Green
        foreach ($proc in $processes) {
            Write-Host "  PID: $($proc.Id)" -ForegroundColor White
            Write-Host "  启动时间: $($proc.StartTime.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor White
            $memMB = [math]::Round($proc.WorkingSet64 / 1MB, 2)
            Write-Host "  内存使用: $memMB MB" -ForegroundColor White
            Write-Host "  CPU时间: $($proc.CPU)" -ForegroundColor White
        }
    } else {
        Write-Host "状态: 未运行" -ForegroundColor Red
    }
    
    # 检查日志文件
    Write-Host ""
    if (Test-Path $LogFile) {
        $logInfo = Get-Item $LogFile
        Write-Host "日志文件: $LogFile" -ForegroundColor Cyan
        $sizeKB = [math]::Round($logInfo.Length / 1KB, 2)
        Write-Host "  大小: $sizeKB KB" -ForegroundColor White
        Write-Host "  最后修改: $($logInfo.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor White
        
        # 显示最后一条日志
        try {
            $lastLine = Get-Content $LogFile -Tail 1 -ErrorAction SilentlyContinue
            if ($lastLine) {
                Write-Host "  最后日志: $lastLine" -ForegroundColor Gray
            }
        } catch {
            # 忽略读取错误
        }
    } else {
        Write-Host "日志文件: 不存在" -ForegroundColor Yellow
    }
    
    # 检查脚本文件
    Write-Host ""
    if (Test-Path $ScriptPath) {
        $scriptInfo = Get-Item $ScriptPath
        Write-Host "脚本文件: $ScriptPath" -ForegroundColor Cyan
        Write-Host "  最后修改: $($scriptInfo.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor White
    } else {
        Write-Host "脚本文件: 不存在" -ForegroundColor Red
    }
    
    Write-Host ""
}

# 显示日志
function Show-XlsxConverterLogs {
    if (-not (Test-Path $LogFile)) {
        Write-Host "日志文件不存在: $LogFile" -ForegroundColor Red
        return
    }
    
    Write-Host ""
    Write-Host "=== $ScriptName 日志 (最后30行) ===" -ForegroundColor Cyan
    Write-Host ""
    
    try {
        Get-Content $LogFile -Tail 30 -Encoding UTF8
    } catch {
        Write-Host "读取日志失败: $_" -ForegroundColor Red
    }
    
    Write-Host ""
    Write-Host "提示: 使用 'Get-Content $LogFile -Tail 100' 查看更多日志" -ForegroundColor Gray
    Write-Host ""
}

# 安装服务（创建计划任务）
function Install-XlsxConverter {
    Write-Host "安装功能暂未实现" -ForegroundColor Yellow
    Write-Host "提示: 可以使用Windows计划任务或服务来设置开机自启动" -ForegroundColor Cyan
}

# 卸载服务
function Uninstall-XlsxConverter {
    Write-Host "卸载功能暂未实现" -ForegroundColor Yellow
}

# 主逻辑
switch ($Action) {
    "start" { Start-XlsxConverter }
    "stop" { Stop-XlsxConverter }
    "restart" { Restart-XlsxConverter }
    "status" { Show-XlsxConverterStatus }
    "logs" { Show-XlsxConverterLogs }
    "install" { Install-XlsxConverter }
    "uninstall" { Uninstall-XlsxConverter }
    default {
        Write-Host "用法: .\manage.ps1 [start|stop|restart|status|logs]" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "命令:" -ForegroundColor Cyan
        Write-Host "  start   - 启动服务" -ForegroundColor White
        Write-Host "  stop    - 停止服务" -ForegroundColor White
        Write-Host "  restart - 重启服务" -ForegroundColor White
        Write-Host "  status  - 查看状态" -ForegroundColor White
        Write-Host "  logs    - 查看日志" -ForegroundColor White
    }
}
