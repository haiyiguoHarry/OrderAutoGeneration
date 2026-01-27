using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LicenseIssuerTool;

/// <summary>
/// 说明：
/// - 这是“发行方（你）”自用的离线签发工具，不要发给客户
/// - 用你的私钥 PEM 对 license 进行签名，输出 license.json
/// </summary>
internal static class Program
{
    public static int Main(string[] args)
    {
        var arg = ParseArgs(args);

        if (arg.ContainsKey("help") || args.Length == 0)
        {
            PrintHelp();
            return 0;
        }

        if (!arg.TryGetValue("privateKeyPem", out var privateKeyPemPath) || string.IsNullOrWhiteSpace(privateKeyPemPath))
        {
            Console.Error.WriteLine("缺少 --privateKeyPem <path>");
            return 2;
        }

        if (!File.Exists(privateKeyPemPath))
        {
            Console.Error.WriteLine($"私钥文件不存在: {privateKeyPemPath}");
            return 2;
        }

        if (!arg.TryGetValue("fingerprint", out var fingerprint) || string.IsNullOrWhiteSpace(fingerprint))
        {
            Console.Error.WriteLine("缺少 --fingerprint <sha256Hex>（客户侧用 OrderConverterEXE.exe -- --print-fingerprint 获取）");
            return 2;
        }

        if (!arg.TryGetValue("validToUtc", out var validToUtcStr) || string.IsNullOrWhiteSpace(validToUtcStr))
        {
            Console.Error.WriteLine("缺少 --validToUtc <UTC时间>，例如 2026-03-31T23:59:59Z");
            return 2;
        }

        if (!DateTime.TryParse(validToUtcStr, null, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var validToUtc))
        {
            Console.Error.WriteLine("validToUtc 不是合法 UTC ISO-8601 时间");
            return 2;
        }

        var validFromUtc = DateTime.UtcNow;
        if (arg.TryGetValue("validFromUtc", out var validFromUtcStr) && !string.IsNullOrWhiteSpace(validFromUtcStr))
        {
            if (!DateTime.TryParse(validFromUtcStr, null, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out validFromUtc))
            {
                Console.Error.WriteLine("validFromUtc 不是合法 UTC ISO-8601 时间");
                return 2;
            }
        }

        var license = new LicenseRecord
        {
            SchemaVersion = 1,
            LicenseId = arg.GetValueOrDefault("licenseId") ?? $"LIC-{DateTime.UtcNow:yyyy}-{Guid.NewGuid():N}".ToUpperInvariant(),
            IssuedTo = arg.GetValueOrDefault("issuedTo") ?? "UNKNOWN",
            ValidFromUtc = validFromUtc.ToString("O"),
            ValidToUtc = validToUtc.ToString("O"),
            MachineBinding = new LicenseRecord.MachineBindingRecord
            {
                FingerprintSha256 = fingerprint.Trim()
            },
            Issuer = new LicenseRecord.IssuerRecord
            {
                Name = arg.GetValueOrDefault("issuerName") ?? "",
                IdHash = arg.GetValueOrDefault("issuerIdHash") ?? ""
            },
            Signature = ""
        };

        var data = CanonicalLicenseSerializer.SerializeForSigning(license);
        var signature = Sign(privateKeyPemPath, data);
        license = license with { Signature = Convert.ToBase64String(signature) };

        var output = arg.GetValueOrDefault("out") ?? Path.Combine(Environment.CurrentDirectory, "license.json");
        var json = JsonSerializer.Serialize(license, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(output, json, Encoding.UTF8);

        Console.WriteLine($"已生成: {output}");
        Console.WriteLine($"licenseId: {license.LicenseId}");
        Console.WriteLine($"validToUtc: {license.ValidToUtc}");
        return 0;
    }

    private static byte[] Sign(string privateKeyPemPath, byte[] data)
    {
        var pem = File.ReadAllText(privateKeyPemPath, Encoding.UTF8);
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(pem);
        return ecdsa.SignData(data, HashAlgorithmName.SHA256);
    }

    private static Dictionary<string, string> ParseArgs(string[] args)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (!a.StartsWith("--")) continue;
            var key = a[2..];
            var val = (i + 1 < args.Length && !args[i + 1].StartsWith("--")) ? args[++i] : "true";
            dict[key] = val;
        }
        return dict;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
LicenseIssuerTool（发行方离线签发工具）

用法：
  dotnet run --project OrderConverterEXE/Tools/LicenseIssuerTool/LicenseIssuerTool.csproj -- ^
    --privateKeyPem "E:\keys\issuer-private-key.pem" ^
    --fingerprint "<客户机器fingerprintSha256Hex>" ^
    --validToUtc "2026-03-31T23:59:59Z" ^
    --issuedTo "客户公司/项目" ^
    --issuerName "你的姓名" ^
    --issuerIdHash "<SHA256(身份证号+salt)截断>" ^
    --out "E:\out\license.json"

说明：
  - 私钥 PEM 仅保留在你手里，不要提交代码库/不要发给客户
  - fingerprint 来自客户侧：OrderConverterEXE.exe --print-fingerprint
""");
    }
}

