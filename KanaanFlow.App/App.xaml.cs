using KanaanFlow.App.Localization;
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
    private readonly Task dbInitTask;

    public App(DbInitializer dbInitializerInstance, ILicenseService licenseServiceInstance, ILogger<App> loggerInstance)
    {
        LocalizationManager.Instance.LoadSavedLanguage();

        InitializeComponent();

        dbInitializer = dbInitializerInstance;
        licenseService = licenseServiceInstance;
        logger = loggerInstance;

        dbInitTask = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await dbInitializer.InitializeAsync(CancellationToken.None);
        logger.LogInformation("Database initialized successfully.");
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        Shell shell = new AppShell();
        shell.FlowDirection = LocalizationManager.Instance.IsRtl
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;

        Window window = new(shell);

        _ = GateAsync().ContinueWith(t =>
        {
            if (t.IsFaulted)
                logger.LogError(t.Exception, "License gate navigation failed.");
        }, TaskScheduler.Default);

        return window;
    }

    private async Task GateAsync()
    {
        await dbInitTask;

        LicenseStatus status = await licenseService.GetStatusAsync(CancellationToken.None);

        if (status.IsValid)
            await Shell.Current.GoToAsync("//Dashboard");
        else
            await Shell.Current.GoToAsync("//License");
    }
}
