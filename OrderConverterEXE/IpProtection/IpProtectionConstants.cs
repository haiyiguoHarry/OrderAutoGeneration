using System.Security.Cryptography;
using System.Text;

namespace OrderConverterEXE.IpProtection;

internal static class IpProtectionConstants
{
    /// <summary>
    /// 内置“根公钥”（Root Public Key）。
    ///
    /// 说明：
    /// - 这是授权体系的根信任，必须内置，否则外置公钥文件可被替换绕过。
    /// - 如需“从 exe 目录读取 issuer-public-key.pem”，请使用“带签名的外置公钥”机制：
    ///   issuer-public-key.pem + issuer-public-key.pem.sig，并用该 Root 公钥验证 sig。
    /// </summary>
    public const string RootPublicKeyPem = """
-----BEGIN PUBLIC KEY-----
MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEi4TV99xpm5ifhh9B8PrzknzJdcBI
gZz9I84nag1whPSE5Gv/QFn7yljcseL1nyCwN6P7zHMr+Q+qO/mHDINlcA==
-----END PUBLIC KEY-----
""";

    public const string ProductName = "OrderConverterEXE";

    public static string ProgramDataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), ProductName);

    public static string DefaultLicensePathProgramData =>
        Path.Combine(ProgramDataDir, "license.json");

    public static string DefaultStatePathProgramData =>
        Path.Combine(ProgramDataDir, "state.dat");

    /// <summary>
    /// 外置“工作公钥”文件名（放在 exe 同目录）。
    /// </summary>
    public const string IssuerPublicKeyFileName = "issuer-public-key.pem";

    /// <summary>
    /// 外置“工作公钥”签名文件名（放在 exe 同目录）。
    /// </summary>
    public const string IssuerPublicKeySignatureFileName = "issuer-public-key.pem.sig";

    /// <summary>
    /// 签名 issuer-public-key.pem 时对内容做归一化：转 LF、Trim 末尾空白。
    /// </summary>
    public static byte[] NormalizePemForSigning(string pemText)
    {
        var normalized = pemText
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .TrimEnd();
        return Encoding.UTF8.GetBytes(normalized);
    }

    public static bool VerifyIssuerPublicKeyFileSignature(string pemText, byte[] signatureDer)
    {
        var data = NormalizePemForSigning(pemText);
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(RootPublicKeyPem);
        return ecdsa.VerifyData(data, signatureDer, HashAlgorithmName.SHA256);
    }
}

