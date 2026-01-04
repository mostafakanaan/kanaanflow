namespace KanaanFlow.Core.Licensing;

public sealed class LicenseStatus
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;

    public LicenseClaims? Claims { get; set; }
}
