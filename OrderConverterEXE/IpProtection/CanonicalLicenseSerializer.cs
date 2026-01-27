using System.Text;
using System.Text.Json;

namespace OrderConverterEXE.IpProtection;

/// <summary>
/// 用于生成/校验 license 的规范化（canonical）JSON 序列化。
///
/// 重要：验签依赖字节级一致性。这里采用“固定字段顺序 + 最小 JSON”的方式，
/// 避免不同序列化器/属性顺序导致验签失败。
/// </summary>
public static class CanonicalLicenseSerializer
{
    public static byte[] SerializeForSigning(LicenseRecord license)
    {
        // 只序列化除 signature 外的字段，且字段顺序固定。
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = false,
            SkipValidation = false
        }))
        {
            writer.WriteStartObject();

            writer.WriteNumber("schemaVersion", license.SchemaVersion);
            writer.WriteString("licenseId", license.LicenseId ?? "");
            writer.WriteString("issuedTo", license.IssuedTo ?? "");
            writer.WriteString("validFromUtc", license.ValidFromUtc ?? "");
            writer.WriteString("validToUtc", license.ValidToUtc ?? "");

            writer.WritePropertyName("machineBinding");
            writer.WriteStartObject();
            writer.WriteString("fingerprintSha256", license.MachineBinding?.FingerprintSha256 ?? "");
            writer.WriteEndObject();

            writer.WritePropertyName("issuer");
            writer.WriteStartObject();
            writer.WriteString("name", license.Issuer?.Name ?? "");
            writer.WriteString("idHash", license.Issuer?.IdHash ?? "");
            writer.WriteEndObject();

            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    public static string SerializeForSigningUtf8(LicenseRecord license) =>
        Encoding.UTF8.GetString(SerializeForSigning(license));
}

