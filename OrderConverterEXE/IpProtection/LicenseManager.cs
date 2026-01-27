using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Runtime.Versioning;

namespace OrderConverterEXE.IpProtection;

[SupportedOSPlatform("windows")]
internal sealed class LicenseManager
{
    private readonly ILogger _logger;

    public LicenseManager(ILogger logger)
    {
        _logger = logger;
    }

    public IpGuardResult LoadAndValidate(string? explicitLicensePath = null)
    {
        var licensePath = ResolveLicensePath(explicitLicensePath);
        if (licensePath == null)
        {
            return IpGuardResult.Fail(IpErrorCodes.LicenseNotFound,
                "未找到授权文件 license.json（建议放到 C:\\ProgramData\\OrderConverterEXE\\license.json 或 exe 同目录）");
        }

        LicenseRecord license;
        try
        {
            var json = File.ReadAllText(licensePath, Encoding.UTF8);
            license = JsonSerializer.Deserialize<LicenseRecord>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new LicenseRecord();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取/解析授权文件失败: {Path}", licensePath);
            return IpGuardResult.Fail(IpErrorCodes.LicenseFormatInvalid, "授权文件格式错误或无法读取（license.json）");
        }

        if (!TryParseUtc(license.ValidFromUtc, out var validFromUtc) ||
            !TryParseUtc(license.ValidToUtc, out var validToUtc))
        {
            return IpGuardResult.Fail(IpErrorCodes.LicenseFormatInvalid, "授权文件时间字段无效（validFromUtc/validToUtc 必须为 UTC ISO-8601）");
        }

        // 1) 验签
        var verify = VerifySignature(license);
        if (!verify.Ok)
        {
            return IpGuardResult.Fail(verify.ErrorCode!, verify.Message);
        }

        // 2) 到期校验（UTC）
        var nowUtc = DateTime.UtcNow;
        if (nowUtc < validFromUtc)
        {
            return IpGuardResult.Fail(IpErrorCodes.LicenseExpired, $"授权尚未生效（validFromUtc={validFromUtc:O}）");
        }

        if (nowUtc > validToUtc)
        {
            return IpGuardResult.Fail(IpErrorCodes.LicenseExpired, $"授权已过期（validToUtc={validToUtc:O}），请联系授权方续签");
        }

        // 3) 机器绑定
        var currentFp = MachineFingerprint.GetFingerprintSha256Hex();
        var licenseFp = (license.MachineBinding?.FingerprintSha256 ?? "").Trim();
        if (!string.Equals(currentFp, licenseFp, StringComparison.OrdinalIgnoreCase))
        {
            return IpGuardResult.Fail(IpErrorCodes.MachineMismatch,
                $"机器不匹配（fingerprint={currentFp[..Math.Min(12, currentFp.Length)]}...），请联系授权方重新签发授权");
        }

        _logger.LogInformation("授权校验通过: licenseId={LicenseId}, issuedTo={IssuedTo}, validToUtc={ValidToUtc}, fingerprintPrefix={Fp}",
            license.LicenseId, license.IssuedTo, license.ValidToUtc, currentFp[..Math.Min(12, currentFp.Length)]);

        return IpGuardResult.Ok(license);
    }

    private (bool Ok, string? ErrorCode, string Message) VerifySignature(LicenseRecord license)
    {
        var publicKeyPem = ResolveIssuerPublicKeyPem();
        if (string.IsNullOrWhiteSpace(publicKeyPem))
            return (false, IpErrorCodes.PublicKeyNotConfigured, "未配置发行方公钥（RootPublicKeyPem 为空）");

        if (string.IsNullOrWhiteSpace(license.Signature))
        {
            return (false, IpErrorCodes.LicenseSignatureInvalid, "授权文件缺少 signature");
        }

        byte[] signatureBytes;
        try
        {
            signatureBytes = Convert.FromBase64String(license.Signature);
        }
        catch
        {
            return (false, IpErrorCodes.LicenseSignatureInvalid, "授权文件 signature 不是有效的 Base64");
        }

        byte[] data = CanonicalLicenseSerializer.SerializeForSigning(license);

        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(publicKeyPem);
            var ok = ecdsa.VerifyData(data, signatureBytes, HashAlgorithmName.SHA256);
            return ok
                ? (true, null, "OK")
                : (false, IpErrorCodes.LicenseSignatureInvalid, "授权文件验签失败（signature 无效）");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "验签异常");
            return (false, IpErrorCodes.LicenseSignatureInvalid, "授权文件验签异常（公钥格式/算法不匹配）");
        }
    }

    /// <summary>
    /// 发行方“工作公钥”来源：
    /// 1) exe 同目录 issuer-public-key.pem + issuer-public-key.pem.sig（用 RootPublicKeyPem 验签）
    /// 2) 回退使用 RootPublicKeyPem 本身（最小可用）
    /// </summary>
    private string ResolveIssuerPublicKeyPem()
    {
        try
        {
            var exeDir = AppContext.BaseDirectory;
            var pemPath = Path.Combine(exeDir, IpProtectionConstants.IssuerPublicKeyFileName);
            var sigPath = Path.Combine(exeDir, IpProtectionConstants.IssuerPublicKeySignatureFileName);

            if (File.Exists(pemPath) && File.Exists(sigPath))
            {
                var pemText = File.ReadAllText(pemPath, Encoding.UTF8);
                var sigDer = File.ReadAllBytes(sigPath);

                if (IpProtectionConstants.VerifyIssuerPublicKeyFileSignature(pemText, sigDer))
                {
                    _logger.LogInformation("已加载外置发行方公钥: {File}", pemPath);
                    return pemText;
                }

                _logger.LogWarning("外置发行方公钥签名校验失败，将回退使用内置 RootPublicKeyPem。sigFile={SigFile}", sigPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取外置发行方公钥失败，将回退使用内置 RootPublicKeyPem。");
        }

        return IpProtectionConstants.RootPublicKeyPem;
    }

    private static bool TryParseUtc(string value, out DateTime utc)
    {
        utc = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (!DateTime.TryParse(value, null,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var dt))
        {
            return false;
        }
        utc = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        return true;
    }

    private static string? ResolveLicensePath(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
            return explicitPath;

        // ProgramData 优先
        var p1 = IpProtectionConstants.DefaultLicensePathProgramData;
        if (File.Exists(p1)) return p1;

        // exe 同目录
        var exeDir = AppContext.BaseDirectory;
        var p2 = Path.Combine(exeDir, "license.json");
        if (File.Exists(p2)) return p2;

        return null;
    }
}

