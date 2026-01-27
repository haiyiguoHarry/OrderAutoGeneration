using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OrderConverterEXE.IpProtection;

internal sealed class TimeTamperDetector
{
    private static readonly byte[] Entropy = SHA256.HashData(Encoding.UTF8.GetBytes("OrderConverterEXE|IpProtection|state|v1"));

    private readonly string _statePath;
    private readonly TimeSpan _rollbackTolerance;

    public TimeTamperDetector(string statePath, TimeSpan rollbackTolerance)
    {
        _statePath = statePath;
        _rollbackTolerance = rollbackTolerance;
    }

    public (bool Ok, string? ErrorCode, string Message) CheckAndUpdate(DateTime nowUtc, LicenseRecord license)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);

        IpState? state = null;
        if (File.Exists(_statePath))
        {
            try
            {
                state = ReadState();
            }
            catch
            {
                return (false, IpErrorCodes.StateTamperedOrUnreadable, "IP 状态文件无法读取/可能被篡改（state.dat）");
            }
        }

        var lastSeenUtc = ParseUtcOrNull(state?.LastSeenUtc);
        if (lastSeenUtc.HasValue)
        {
            if (nowUtc < lastSeenUtc.Value - _rollbackTolerance)
            {
                return (false, IpErrorCodes.TimeRollbackDetected,
                    $"检测到系统时间回拨（lastSeenUtc={lastSeenUtc:O}, nowUtc={nowUtc:O}），请校准时间并联系授权方");
            }
        }

        // 防止 license 回滚：如果已经见过更新的 licenseId 或更晚的 validTo，则拒绝使用更旧的。
        var lastValidToUtc = ParseUtcOrNull(state?.LastLicenseValidToUtc);
        var currentValidToUtc = ParseUtcOrNull(license.ValidToUtc);
        if (lastValidToUtc.HasValue && currentValidToUtc.HasValue && currentValidToUtc.Value < lastValidToUtc.Value)
        {
            return (false, IpErrorCodes.StateTamperedOrUnreadable,
                $"检测到授权回滚（当前validTo={currentValidToUtc:O} < 历史validTo={lastValidToUtc:O}），请使用最新授权文件");
        }

        var next = new IpState
        {
            LastSeenUtc = (lastSeenUtc.HasValue ? (nowUtc > lastSeenUtc ? nowUtc : lastSeenUtc.Value) : nowUtc).ToString("O"),
            LastLicenseId = license.LicenseId ?? "",
            LastLicenseValidToUtc = license.ValidToUtc ?? ""
        };

        try
        {
            WriteState(next);
        }
        catch
        {
            // 写 state 失败不应直接放行（否则回拨检测失效），但也不希望误伤太多。
            return (false, IpErrorCodes.StateTamperedOrUnreadable, "无法写入 IP 状态文件（state.dat），请检查权限（建议以管理员运行/确保 ProgramData 可写）");
        }

        return (true, null, "OK");
    }

    private IpState ReadState()
    {
        var cipher = File.ReadAllBytes(_statePath);
        var plain = Dpapi.Unprotect(cipher, Entropy);
        var json = Encoding.UTF8.GetString(plain);
        return JsonSerializer.Deserialize<IpState>(json, JsonOptions()) ?? new IpState();
    }

    private void WriteState(IpState state)
    {
        var json = JsonSerializer.Serialize(state, JsonOptions());
        var plain = Encoding.UTF8.GetBytes(json);
        var cipher = Dpapi.Protect(plain, Entropy);
        File.WriteAllBytes(_statePath, cipher);
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false
    };

    private static DateTime? ParseUtcOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var dt))
        {
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }
        return null;
    }
}

