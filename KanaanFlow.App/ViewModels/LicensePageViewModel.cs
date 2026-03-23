namespace KanaanFlow.App.ViewModels;

using CommunityToolkit.Mvvm.Input;
using KanaanFlow.Core.Licensing;
using System;
using System.Threading;
using System.Threading.Tasks;

public sealed partial class LicensePageViewModel : BaseViewModel
{
    private readonly ILicenseService licenseService;

    private string licenseKey = string.Empty;
    private string statusMessage = string.Empty;
    private bool isLicenseValid;

    public event EventHandler? LicenseValidated;

    public string LicenseKey
    {
        get => licenseKey;
        set => SetProperty(ref licenseKey, value);
    }

    public string StatusMessage
    {
        get => statusMessage;
        set => SetProperty(ref statusMessage, value);
    }

    public bool IsLicenseValid
    {
        get => isLicenseValid;
        set => SetProperty(ref isLicenseValid, value);
    }

    public LicensePageViewModel(ILicenseService licenseServiceInstance)
    {
        licenseService = licenseServiceInstance;
        Title = "Activate License";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            await licenseService.SaveLicenseKeyAsync(LicenseKey, CancellationToken.None);
            LicenseStatus status = await licenseService.GetStatusAsync(CancellationToken.None);
            StatusMessage = status.Message;
            IsLicenseValid = status.IsValid;

            if (status.IsValid)
            {
                LicenseValidated?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed: " + ex.Message;
        }
    }
}
