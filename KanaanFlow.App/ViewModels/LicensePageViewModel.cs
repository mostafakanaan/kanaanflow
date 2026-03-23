namespace KanaanFlow.App.ViewModels;

using KanaanFlow.Core.Licensing;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

public sealed class LicensePageViewModel : INotifyPropertyChanged
{
    private readonly ILicenseService licenseService;

    private string licenseKey = string.Empty;
    private string statusMessage = string.Empty;
    private bool isLicenseValid;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? LicenseValidated;

    public string LicenseKey
    {
        get => licenseKey;
        set { licenseKey = value; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => statusMessage;
        set { statusMessage = value; OnPropertyChanged(); }
    }

    public bool IsLicenseValid
    {
        get => isLicenseValid;
        set { isLicenseValid = value; OnPropertyChanged(); }
    }

    public ICommand SaveCommand { get; }

    public LicensePageViewModel(ILicenseService licenseServiceInstance)
    {
        licenseService = licenseServiceInstance;
        SaveCommand = new Command(async () => await SaveAsync());
    }

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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
