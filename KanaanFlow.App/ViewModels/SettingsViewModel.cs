namespace KanaanFlow.App.ViewModels;

using CommunityToolkit.Mvvm.Input;
using KanaanFlow.Core.Licensing;
using System.Threading;
using System.Threading.Tasks;

public sealed partial class SettingsViewModel : BaseViewModel
{
    private readonly ILicenseService licenseService;

    private string licenseInfo = string.Empty;
    private string appVersion = "1.0.0";

    public string LicenseInfo
    {
        get => licenseInfo;
        set => SetProperty(ref licenseInfo, value);
    }

    public string AppVersion
    {
        get => appVersion;
        set => SetProperty(ref appVersion, value);
    }

    public SettingsViewModel(ILicenseService licenseServiceInstance)
    {
        licenseService = licenseServiceInstance;
        Title = "Settings";
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        LicenseStatus status = await licenseService.GetStatusAsync(CancellationToken.None);

        if (status.IsValid && status.Claims != null)
        {
            LicenseInfo = "Licensed to: " + status.Claims.Customer + " | Valid until: " + status.Claims.ValidUntilUtc.ToString("yyyy-MM-dd");
        }
        else
        {
            LicenseInfo = "No valid license - " + status.Message;
        }
    }
}
