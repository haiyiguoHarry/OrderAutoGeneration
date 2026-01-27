using System.Text.Json.Serialization;

namespace OrderConverterEXE.IpProtection;

internal sealed record IpState
{
    [JsonPropertyName("lastSeenUtc")]
    public string LastSeenUtc { get; init; } = "";

    [JsonPropertyName("lastLicenseId")]
    public string LastLicenseId { get; init; } = "";

    [JsonPropertyName("lastLicenseValidToUtc")]
    public string LastLicenseValidToUtc { get; init; } = "";
}

