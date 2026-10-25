using KanaanFlow.Web;
using KanaanFlow.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton(sp => new HttpClient { Timeout = TimeSpan.FromSeconds(20) });
builder.Services.AddSingleton<LocalStore>();
builder.Services.AddSingleton<BookService>();
builder.Services.AddSingleton<ToastService>();
builder.Services.AddSingleton<ApiClient>();
builder.Services.AddSingleton<SyncService>();
builder.Services.AddSingleton<LocalAuth>();
builder.Services.AddSingleton<RateService>();
builder.Services.AddSingleton<ExportService>();

WebAssemblyHost host = builder.Build();

// Load this device's copy of the book before the first screen renders, then start syncing in the background.
await host.Services.GetRequiredService<BookService>().LoadAsync();
await host.Services.GetRequiredService<SyncService>().StartAsync();

await host.RunAsync();
