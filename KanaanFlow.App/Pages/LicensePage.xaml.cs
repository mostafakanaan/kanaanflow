namespace KanaanFlow.App.Pages;

using KanaanFlow.Core.Licensing;
using System;
using System.Threading;

public partial class LicensePage : ContentPage
{
    private readonly ILicenseService licenseService;

    public LicensePage(ILicenseService licenseServiceInstance)
    {
        InitializeComponent();
        licenseService = licenseServiceInstance;
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            await licenseService.SaveLicenseKeyAsync(KeyEditor.Text ?? string.Empty, CancellationToken.None);

            LicenseStatus status = await licenseService.GetStatusAsync(CancellationToken.None);
            StatusLabel.Text = status.Message;

            if (status.IsValid)
            {
                await Shell.Current.GoToAsync("//Welcome");
            }
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "Failed: " + ex.Message;
        }
    }
}
