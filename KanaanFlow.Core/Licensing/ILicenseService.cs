namespace KanaanFlow.Core.Licensing;

using System.Threading;
using System.Threading.Tasks;

public interface ILicenseService
{
    Task<LicenseStatus> GetStatusAsync(CancellationToken cancellationToken);

    Task SaveLicenseKeyAsync(string licenseKey, CancellationToken cancellationToken);

    Task<string?> LoadLicenseKeyAsync(CancellationToken cancellationToken);
}
