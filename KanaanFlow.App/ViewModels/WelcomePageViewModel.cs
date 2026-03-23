namespace KanaanFlow.App.ViewModels;

using CommunityToolkit.Mvvm.Input;
using KanaanFlow.Core.Licensing;
using System.Threading;
using System.Threading.Tasks;

public sealed partial class WelcomePageViewModel : BaseViewModel
{
    private readonly ILicenseService licenseService;

    private string customer = string.Empty;
    private string validUntil = string.Empty;
    private bool shouldRedirectToLicense;

    public string Customer
    {
        get => customer;
        set => SetProperty(ref customer, value);
    }

    public string ValidUntil
    {
        get => validUntil;
        set => SetProperty(ref validUntil, value);
    }

    public bool ShouldRedirectToLicense
    {
        get => shouldRedirectToLicense;
        set => SetProperty(ref shouldRedirectToLicense, value);
    }

    public WelcomePageViewModel(ILicenseService licenseServiceInstance)
    {
        licenseService = licenseServiceInstance;
        Title = "Welcome";
    }

    [RelayCommand]
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
}
