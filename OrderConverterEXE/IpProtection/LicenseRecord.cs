using System.Text.Json.Serialization;

namespace OrderConverterEXE.IpProtection;

public sealed record LicenseRecord
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = 1;

    [JsonPropertyName("licenseId")]
    public string LicenseId { get; init; } = "";

    [JsonPropertyName("issuedTo")]
    public string IssuedTo { get; init; } = "";

    [JsonPropertyName("validFromUtc")]
    public string ValidFromUtc { get; init; } = "";

    [JsonPropertyName("validToUtc")]
    public string ValidToUtc { get; init; } = "";

    [JsonPropertyName("machineBinding")]
    public MachineBindingRecord MachineBinding { get; init; } = new();

    [JsonPropertyName("issuer")]
    public IssuerRecord Issuer { get; init; } = new();

    [JsonPropertyName("signature")]
    public string Signature { get; init; } = "";

    public sealed record MachineBindingRecord
    {
        [JsonPropertyName("fingerprintSha256")]
        public string FingerprintSha256 { get; init; } = "";
    }

    public sealed record IssuerRecord
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = "";

        [JsonPropertyName("idHash")]
        public string IdHash { get; init; } = "";
    }
}

