using XeroPayslipDownloader;
using XeroPayslipDownloader.Options;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

builder.Services.Configure<DownloadOptions>(builder.Configuration.GetSection(DownloadOptions.Download));

// Install the browser required by Playwright 
Microsoft.Playwright.Program.Main(["install", "firefox"]);

var host = builder.Build();
host.Run();
