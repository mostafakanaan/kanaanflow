namespace KanaanFlow.App.Pages;

using KanaanFlow.Core.Licensing;
using System.Threading;

public partial class WelcomePage : ContentPage
{
    private readonly ILicenseService licenseService;

    public WelcomePage(ILicenseService licenseServiceInstance)
    {
        InitializeComponent();
        licenseService = licenseServiceInstance;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        LicenseStatus status = await licenseService.GetStatusAsync(CancellationToken.None);

        if (!status.IsValid || status.Claims == null)
        {
            await Shell.Current.GoToAsync("//License");
            return;
        }

        CustomerLabel.Text = "Customer: " + status.Claims.Customer;
        ValidUntilLabel.Text = "Valid until (UTC): " + status.Claims.ValidUntilUtc.ToString("u");
    }
}
