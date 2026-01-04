namespace KanaanFlow.Core.Licensing;

using System;

public sealed class LicenseClaims
{
    public string LicenseId { get; set; } = string.Empty;
    public string Customer { get; set; } = string.Empty;
    public DateTime ValidUntilUtc { get; set; }
}
