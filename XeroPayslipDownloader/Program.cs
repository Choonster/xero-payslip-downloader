using XeroPayslipDownloader;
using XeroPayslipDownloader.Options;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

builder.Services.Configure<DownloadOptions>(builder.Configuration.GetSection(DownloadOptions.Download));

// Install the browser required by Playwright 
#pragma warning disable CA1861 // Avoid constant arrays as arguments
Microsoft.Playwright.Program.Main(new[] { "install", "firefox" });
#pragma warning restore CA1861 // Avoid constant arrays as arguments

var host = builder.Build();
host.Run();
