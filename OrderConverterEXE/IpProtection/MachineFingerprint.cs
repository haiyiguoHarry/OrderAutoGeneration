using Microsoft.Win32;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.Versioning;

namespace OrderConverterEXE.IpProtection;

[SupportedOSPlatform("windows")]
public static class MachineFingerprint
{
    public static string GetFingerprintSha256Hex()
    {
        var machineGuid = ReadMachineGuid() ?? "";
        var computerName = Environment.MachineName ?? "";

        // 组合材料（可按需加入卷序列等），最后只输出 SHA256（不泄露原始材料）
        var raw = $"{machineGuid}|{computerName}".Trim().ToUpperInvariant();
        var bytes = Encoding.UTF8.GetBytes(raw);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash); // HEX uppercase
    }

    private static string? ReadMachineGuid()
    {
        // HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            return key?.GetValue("MachineGuid")?.ToString();
        }
        catch
        {
            return null;
        }
    }
}

