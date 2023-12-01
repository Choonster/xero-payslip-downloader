# Xero Payslip Downloader

Command-line application that uses Playwright to download employee payslips from Xero.

## Configuration

Set the `Download:DownloadPathFormat` app setting to the path you want the payslips downloaded to. This supports the following placeholders:

- `~` - When this is the first character, it will be replaced with the user directory
- `{Payee}` - The name of the payee
- `{Date}` - The end date of the pay period
- `{Id}` - The ID of the payslip in Xero

## Usage

Run the application and wait for the browser to open to the Xero login page, then log in. Once logged in, all payslips will be downloaded to the configured path.

Any payslips that already exist in the configured path won't be downloaded again.
