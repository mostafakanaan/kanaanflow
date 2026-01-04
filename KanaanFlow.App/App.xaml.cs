using KanaanFlow.Core.Licensing;
using KanaanFlow.Data.Db;
using Microsoft.Maui.Controls;

namespace KanaanFlow.App;

public partial class App : Application
{
    private readonly DbInitializer dbInitializer;
    private readonly ILicenseService licenseService;

    public App(DbInitializer dbInitializerInstance, ILicenseService licenseServiceInstance)
    {
        InitializeComponent();

        dbInitializer = dbInitializerInstance;
        licenseService = licenseServiceInstance;

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            await dbInitializer.InitializeAsync(CancellationToken.None);
        }
        catch
        {
            // TODO: log
        }
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        Shell shell = new AppShell();

        Window window = new(shell);

        _ = GateAsync();

        return window;
    }

    private async Task GateAsync()
    {
        await Task.Delay(50);

        LicenseStatus status = await licenseService.GetStatusAsync(CancellationToken.None);

        if (status.IsValid)
            await Shell.Current.GoToAsync("//Welcome");
        else
            await Shell.Current.GoToAsync("//License");
    }
}
