using Microsoft.Extensions.DependencyInjection;
using KanaanFlow.Data.Db;

namespace KanaanFlow.App;

public partial class App : Application
{
    public App(DbInitializer dbInitializer)
    {
        InitializeComponent();

        _ = InitializeAsync(dbInitializer);
    }

    private static async Task InitializeAsync(DbInitializer dbInitializer)
    {
        try
        {
            await dbInitializer.InitializeAsync(CancellationToken.None);
        }
        catch
        {
            // TODO: Add logging/UI toast
        }
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}