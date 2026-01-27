using System.Text;
using System.Text.Json;

namespace LicenseIssuerTool;

internal static class CanonicalLicenseSerializer
{
    public static byte[] SerializeForSigning(LicenseRecord license)
    {
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

