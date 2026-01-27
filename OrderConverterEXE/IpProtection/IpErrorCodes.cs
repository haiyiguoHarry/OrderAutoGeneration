namespace OrderConverterEXE.IpProtection;

public static class IpErrorCodes
{
    public const string LicenseNotFound = "IP-0001";
    public const string LicenseSignatureInvalid = "IP-0002";
    public const string LicenseExpired = "IP-0003";
    public const string MachineMismatch = "IP-0004";
    public const string TimeRollbackDetected = "IP-0005";
    public const string LicenseFormatInvalid = "IP-0006";
    public const string StateTamperedOrUnreadable = "IP-0007";
    public const string PublicKeyNotConfigured = "IP-0008";
}

