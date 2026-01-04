namespace KanaanFlow.Core.Licensing;

public sealed class LicenseFile
{
    public LicenseClaims Claims { get; set; } = new LicenseClaims();

    // Base64 signature over canonical payload string (see verifier)
    public string SignatureBase64 { get; set; } = string.Empty;
}
