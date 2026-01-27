namespace OrderConverterEXE.IpProtection;

public sealed record IpGuardResult(
    bool Allowed,
    string? ErrorCode,
    string Message,
    LicenseRecord? License = null
)
{
    public static IpGuardResult Ok(LicenseRecord license) => new(true, null, "OK", license);

    public static IpGuardResult Fail(string errorCode, string message) =>
        new(false, errorCode, message, null);
}

