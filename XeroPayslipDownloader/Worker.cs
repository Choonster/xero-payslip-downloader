using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using System.Globalization;
using System.Text.RegularExpressions;
using XeroPayslipDownloader.DTO;
using XeroPayslipDownloader.Options;

namespace XeroPayslipDownloader
{
    public partial class Worker(IHostApplicationLifetime hostApplicationLifetime, IOptions<DownloadOptions> downloadOptions, ILogger<Worker> logger) : BackgroundService
    {
        private const string PayRunHistoryUrl = "https://payroll.xero.com/EmployeePortal/PayRunHistory";
        private const string PayRunHistoryListAjaxUrl = "https://payroll.xero.com/EmployeePortal/PayRunHistory/ListAjax";
        private const string PrintPaySlipUrlTemplate = "https://payroll.xero.com/EmployeePortal/PayRunHistory/PrintPaySlip/{0}?payeeID={1}";

        [GeneratedRegex(@"\{(.+?)\}")]
        private partial Regex PlaceholderRegex();

        private readonly DownloadOptions downloadOptions = downloadOptions.Value;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var playwright = await Playwright.CreateAsync();

            await using var browser = await playwright.Firefox.LaunchAsync(new BrowserTypeLaunchOptions { Headless = false }).ConfigureAwait(false);
            var context = await browser.NewContextAsync().ConfigureAwait(false);
            var page = await context.NewPageAsync().ConfigureAwait(false);

            // Initial request will redirect to login page, then redirect back to this URL once the user logs in
            await page.GotoAsync(PayRunHistoryUrl).ConfigureAwait(false);

            logger.LogInformation("Waiting for user login");

            await page.WaitForURLAsync(url => url.StartsWith(PayRunHistoryUrl), new PageWaitForURLOptions { Timeout = 0 }).ConfigureAwait(false);

            var legacyPaging = await page.EvaluateHandleAsync("XERO.all.Paging.cache.first()").ConfigureAwait(false);
            var pageCount = await page.EvaluateAsync<int>("(paging) => paging.pageNumberStore.count()", legacyPaging).ConfigureAwait(false);

            for (var pageNumber = 1; pageNumber <= pageCount; pageNumber++)
            {
                logger.LogInformation("Loading page {PageNumber}/{PageCount}", pageNumber, pageCount);

                var waitForRequestTask = page.WaitForRequestAsync(req => req.Url.StartsWith(PayRunHistoryListAjaxUrl));

                await page.EvaluateAsync("([paging, pageNumber]) => { console.log(paging); paging.moveToPage(pageNumber, true) }", new object[] { legacyPaging, pageNumber }).ConfigureAwait(false);

                var request = await waitForRequestTask.ConfigureAwait(false);

                var response = await request.ResponseAsync().ConfigureAwait(false)
                    ?? throw new InvalidOperationException("response is null");

                var payRunHistoryList = await response.JsonAsync<XeroPayRunHistoryListDTO>().ConfigureAwait(false);

                var payRunIndex = 1;
                var payRunCount = payRunHistoryList.Data.Count;

                foreach (var payRun in payRunHistoryList.Data)
                {
                    logger.LogInformation("Processing paylsip {PayRunIndex}/{PayRunCount} - Period ending {Date}", payRunIndex, payRunCount, payRun.Period.EndDate);

                    await DownloadPayslip(context, payRun).ConfigureAwait(false);

                    payRunIndex++;
                }
            }

            logger.LogInformation("Finished downloading payslips, shutting down");

            hostApplicationLifetime.StopApplication();
        }

        private async Task DownloadPayslip(IBrowserContext context, XeroPayRunHistoryDTO payRun)
        {
            var path = ResolvePath(payRun);

            if (File.Exists(path))
            {
                logger.LogInformation("Payslip file already exists at {Path}, skipping", path);
                return;
            }

            var payslipPage = await context.NewPageAsync().ConfigureAwait(false);

            var waitForDownloadTask = payslipPage.WaitForDownloadAsync();

            try
            {
                var url = string.Format(PrintPaySlipUrlTemplate, payRun.PaySlipID, payRun.PayeeID);
                var response = await payslipPage.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.Commit }).ConfigureAwait(false);
            }
            catch (PlaywrightException e) when (e.Message.StartsWith("Download is starting"))
            {
                // Allow download to continue
            }

            var download = await waitForDownloadTask.ConfigureAwait(false);

            await download.SaveAsAsync(path);

            await payslipPage.CloseAsync();

            logger.LogInformation("Downloaded payslip to {Path}", path);
        }

        private string ResolvePath(XeroPayRunHistoryDTO payRun)
        {
            if (string.IsNullOrEmpty(downloadOptions.DownloadPathFormat))
            {
                throw new InvalidOperationException($"{DownloadOptions.Download}.{nameof(DownloadOptions.DownloadPathFormat)} app setting is required");
            }

            var pathSegments = downloadOptions.DownloadPathFormat.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            pathSegments = pathSegments
                .Select(segment =>
                    PlaceholderRegex()
                        .Replace(segment, match =>
                        {
                            if (match.Groups.Count != 2)
                            {
                                return match.Value;
                            }

                            return match.Groups[1].Value switch
                            {
                                "Payee" => payRun.PayeeName,
                                "Date" => payRun.Period.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                                "Id" => payRun.PaySlipID.ToString(CultureInfo.InvariantCulture),
                                _ => match.Value,
                            };
                        })
                )
                .ToArray();

            if (pathSegments[0] == "~")
            {
                pathSegments[0] = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            return Path.Join(pathSegments);
        }
    }
}
