namespace KanaanFlow.Tests;

using KanaanFlow.Core.Licensing;
using Xunit;

public class LicenseClaimsTests
{
    [Fact]
    public void LicenseClaims_DefaultValues_AreEmpty()
    {
        LicenseClaims claims = new LicenseClaims();

        Assert.Equal(string.Empty, claims.LicenseId);
        Assert.Equal(string.Empty, claims.Customer);
        Assert.Equal(default(DateTime), claims.ValidUntilUtc);
    }

    [Fact]
    public void LicenseStatus_DefaultValues_AreCorrect()
    {
        LicenseStatus status = new LicenseStatus();

        Assert.False(status.IsValid);
        Assert.Equal(string.Empty, status.Message);
        Assert.Null(status.Claims);
    }

    [Fact]
    public void LicenseFile_DefaultValues_AreEmpty()
    {
        LicenseFile file = new LicenseFile();

        Assert.NotNull(file.Claims);
        Assert.Equal(string.Empty, file.SignatureBase64);
    }
}
