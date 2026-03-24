using KanaanFlow.App.Licensing;
using KanaanFlow.App.Pages;
using KanaanFlow.App.ViewModels;
using KanaanFlow.Core.Abstractions;
using KanaanFlow.Core.Licensing;
using KanaanFlow.Data.Db;
using KanaanFlow.Data.Repositories;
using KanaanFlow.Data.Services;
using KanaanFlow.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KanaanFlow.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        MauiAppBuilder builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        string dbPath = Path.Combine(FileSystem.AppDataDirectory, "kanaanflow.db3");
        string connectionString = "Filename=" + dbPath;

        builder.Services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlite(connectionString);
        }, ServiceLifetime.Transient);

        // Data
        builder.Services.AddSingleton<DbInitializer>();
        builder.Services.AddTransient<ITransactionRepository, TransactionRepository>();
        builder.Services.AddTransient<ICategoryRepository, CategoryRepository>();
        builder.Services.AddTransient<ILoanRepository, LoanRepository>();

        // Services
        builder.Services.AddTransient<IReportService, ReportService>();
        builder.Services.AddTransient<ISyncService, SyncService>();

        // License
        builder.Services.AddSingleton<ILicenseService, LicenseService>();

        // ViewModels
        builder.Services.AddTransient<LicensePageViewModel>();
        builder.Services.AddTransient<WelcomePageViewModel>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<TransactionListViewModel>();
        builder.Services.AddTransient<AddTransactionViewModel>();
        builder.Services.AddTransient<LoanListViewModel>();
        builder.Services.AddTransient<AddLoanViewModel>();
        builder.Services.AddTransient<CategoryListViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<ReportsViewModel>();

        // Pages
        builder.Services.AddTransient<LicensePage>();
        builder.Services.AddTransient<WelcomePage>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<TransactionListPage>();
        builder.Services.AddTransient<AddTransactionPage>();
        builder.Services.AddTransient<LoanListPage>();
        builder.Services.AddTransient<AddLoanPage>();
        builder.Services.AddTransient<CategoryListPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<ReportsPage>();

        return builder.Build();
    }
}
