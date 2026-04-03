namespace KanaanFlow.App.ViewModels;

using CommunityToolkit.Mvvm.Input;
using KanaanFlow.App.Localization;
using KanaanFlow.Core.Licensing;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

public sealed partial class SettingsViewModel : BaseViewModel
{
    private readonly ILicenseService licenseService;

    private string licenseInfo = string.Empty;
    private string appVersion = "1.0.0";
    private string selectedLanguage = "English";

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

    public string SelectedLanguage
    {
        get => selectedLanguage;
        set
        {
            if (SetProperty(ref selectedLanguage, value))
            {
                ApplyLanguage(value);
            }
        }
    }

    public ObservableCollection<string> Languages { get; } = new ObservableCollection<string>
    {
        "English",
        "العربية"
    };

    public SettingsViewModel(ILicenseService licenseServiceInstance)
    {
        licenseService = licenseServiceInstance;
        Title = LocalizationManager.Instance["Settings"];

        selectedLanguage = LocalizationManager.Instance.CurrentLanguage == "ar" ? "العربية" : "English";
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        LicenseStatus status = await licenseService.GetStatusAsync(CancellationToken.None);

        if (status.IsValid && status.Claims != null)
        {
            LicenseInfo = string.Format(
                LocalizationManager.Instance["Settings"] == "الإعدادات"
                    ? "مرخص لـ: {0} | صالح حتى: {1}"
                    : "Licensed to: {0} | Valid until: {1}",
                status.Claims.Customer,
                status.Claims.ValidUntilUtc.ToString("yyyy-MM-dd"));
        }
        else
        {
            LicenseInfo = (LocalizationManager.Instance.CurrentLanguage == "ar"
                ? "لا يوجد ترخيص صالح - "
                : "No valid license - ") + status.Message;
        }
    }

    private void ApplyLanguage(string displayName)
    {
        string code = displayName == "العربية" ? "ar" : "en";
        LocalizationManager.Instance.SetLanguage(code);

        Title = LocalizationManager.Instance["Settings"];

        if (Application.Current?.MainPage is Shell shell)
        {
            shell.FlowDirection = code == "ar"
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight;
        }

        _ = LoadAsync();
    }
}
