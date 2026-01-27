using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;

namespace OrderConverterEXE.IpProtection;

[SupportedOSPlatform("windows")]
public sealed class IpGuard : IIpGuard
{
    private readonly ILogger<IpGuard> _logger;
    private readonly TimeTamperDetector _timeTamperDetector;

    private DateTime _lastRuntimeCheckUtc = DateTime.MinValue;
    private LicenseRecord? _cachedLicense;

    // 运行中复检间隔：默认 10 分钟（可按需改为配置）
    private static readonly TimeSpan RuntimeRecheckInterval = TimeSpan.FromMinutes(10);

    // 时间回拨容忍：默认 5 分钟
    private static readonly TimeSpan RollbackTolerance = TimeSpan.FromMinutes(5);

    public IpGuard(ILogger<IpGuard> logger)
    {
        _logger = logger;
        _timeTamperDetector = new TimeTamperDetector(IpProtectionConstants.DefaultStatePathProgramData, RollbackTolerance);
    }

    public IpGuardResult ValidateStartup(string? explicitLicensePath = null)
    {
        try
        {
            Directory.CreateDirectory(IpProtectionConstants.ProgramDataDir);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "创建 ProgramData 目录失败: {Dir}", IpProtectionConstants.ProgramDataDir);
        }

        var lm = new LicenseManager(_logger);
        var result = lm.LoadAndValidate(explicitLicensePath);
        if (!result.Allowed || result.License == null)
        {
            _logger.LogWarning("IP 校验失败: {Code} {Message}", result.ErrorCode, result.Message);
            return result;
        }

        var nowUtc = DateTime.UtcNow;
        var timeCheck = _timeTamperDetector.CheckAndUpdate(nowUtc, result.License);
        if (!timeCheck.Ok)
        {
            _logger.LogWarning("IP 时间校验失败: {Code} {Message}", timeCheck.ErrorCode, timeCheck.Message);
            return IpGuardResult.Fail(timeCheck.ErrorCode!, timeCheck.Message);
        }

        _cachedLicense = result.License;
        _lastRuntimeCheckUtc = nowUtc;
        return result;
    }

    public IpGuardResult ValidateRuntime()
    {
        var nowUtc = DateTime.UtcNow;
        if (_cachedLicense != null && (nowUtc - _lastRuntimeCheckUtc) < RuntimeRecheckInterval)
        {
            return IpGuardResult.Ok(_cachedLicense);
        }

        // 运行中复检：重新加载 license（支持续签替换）
        var lm = new LicenseManager(_logger);
        var result = lm.LoadAndValidate(null);
        if (!result.Allowed || result.License == null)
        {
            _logger.LogWarning("IP 运行时复检失败: {Code} {Message}", result.ErrorCode, result.Message);
            return result;
        }

        var timeCheck = _timeTamperDetector.CheckAndUpdate(nowUtc, result.License);
        if (!timeCheck.Ok)
        {
            _logger.LogWarning("IP 运行时时间校验失败: {Code} {Message}", timeCheck.ErrorCode, timeCheck.Message);
            return IpGuardResult.Fail(timeCheck.ErrorCode!, timeCheck.Message);
        }

        _cachedLicense = result.License;
        _lastRuntimeCheckUtc = nowUtc;
        return result;
    }
}

