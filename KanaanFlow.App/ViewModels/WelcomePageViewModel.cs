namespace KanaanFlow.App.ViewModels;

using KanaanFlow.Core.Licensing;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

public sealed class WelcomePageViewModel : INotifyPropertyChanged
{
    private readonly ILicenseService licenseService;

    private string customer = string.Empty;
    private string validUntil = string.Empty;
    private bool shouldRedirectToLicense;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Customer
    {
        get => customer;
        set { customer = value; OnPropertyChanged(); }
    }

    public string ValidUntil
    {
        get => validUntil;
        set { validUntil = value; OnPropertyChanged(); }
    }

    public bool ShouldRedirectToLicense
    {
        get => shouldRedirectToLicense;
        set { shouldRedirectToLicense = value; OnPropertyChanged(); }
    }

    public WelcomePageViewModel(ILicenseService licenseServiceInstance)
    {
        licenseService = licenseServiceInstance;
    }

    public async Task LoadAsync()
    {
        LicenseStatus status = await licenseService.GetStatusAsync(CancellationToken.None);

        if (!status.IsValid || status.Claims == null)
        {
            ShouldRedirectToLicense = true;
            return;
        }

        Customer = "Customer: " + status.Claims.Customer;
        ValidUntil = "Valid until (UTC): " + status.Claims.ValidUntilUtc.ToString("u");
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
