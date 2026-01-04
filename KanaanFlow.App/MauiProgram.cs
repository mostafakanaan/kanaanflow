using KanaanFlow.App.ViewModels;
using KanaanFlow.Core.Abstractions;
using KanaanFlow.Data.Db;
using KanaanFlow.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KanaanFlow.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
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

#if DEBUG
        // write dbPath into console
        Console.WriteLine("Database Path: " + dbPath);

#endif

        builder.Services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlite(connectionString);
        });

        builder.Services.AddSingleton<DbInitializer>();
        builder.Services.AddScoped<INoteRepository, NoteRepository>();

        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<MainPageViewModel>();

        return builder.Build();
	}
}
