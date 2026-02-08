namespace KanaanFlow.App.Pages;

using KanaanFlow.App.ViewModels;
using KanaanFlow.Core.Licensing;
using System.Threading;

public partial class WelcomePage : ContentPage
{
    private readonly ILicenseService licenseService;
    private readonly DashboardViewModel viewModel;

    public WelcomePage(ILicenseService licenseServiceInstance, DashboardViewModel dashboardViewModel)
    {
        InitializeComponent();
        licenseService = licenseServiceInstance;
        viewModel = dashboardViewModel;
        BindingContext = viewModel;

        // Add toolbar item for license info
        ToolbarItems.Add(new ToolbarItem
        {
            Text = "License",
            Command = viewModel.ShowLicenseInfoCommand
        });
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

        CustomerLabel.Text = status.Claims.Customer;
        
        viewModel.CustomerName = status.Claims.Customer;
        viewModel.ValidUntil = status.Claims.ValidUntilUtc.ToString("MMM dd, yyyy");
    }
}
