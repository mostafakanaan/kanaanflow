using KanaanFlow.Core.Licensing;
using KanaanFlow.Data.Db;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;

namespace KanaanFlow.App;

public partial class App : Application
{
    private readonly DbInitializer dbInitializer;
    private readonly ILicenseService licenseService;
    private readonly ILogger<App> logger;

    public App(DbInitializer dbInitializerInstance, ILicenseService licenseServiceInstance, ILogger<App> loggerInstance)
    {
        InitializeComponent();

        dbInitializer = dbInitializerInstance;
        licenseService = licenseServiceInstance;
        logger = loggerInstance;

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            await dbInitializer.InitializeAsync(CancellationToken.None);
            logger.LogInformation("Database initialized successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database initialization failed.");
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
            await Shell.Current.GoToAsync("//Main");
        else
            await Shell.Current.GoToAsync("//License");
    }
}
