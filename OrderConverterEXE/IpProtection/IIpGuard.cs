namespace OrderConverterEXE.IpProtection;

public interface IIpGuard
{
    /// <summary>
    /// 启动前强校验（建议在 Program.cs 调用）。
    /// </summary>
    IpGuardResult ValidateStartup(string? explicitLicensePath = null);

    /// <summary>
    /// 运行中周期复检（建议在后台循环中调用）。
    /// </summary>
    IpGuardResult ValidateRuntime();
}

