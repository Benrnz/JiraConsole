using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Util.Store;

namespace BensJiraConsole;

public class GoogleSheetReader : IWorkSheetReader
{
    private const string ClientSecretsFile = "client_secret_apps.googleusercontent.com.json";
    private static readonly string[] Scopes = [SheetsService.Scope.Spreadsheets];
    private string? googleSheetId;

    private UserCredential? credential;

    private SheetsService? service;

    public async Task<List<List<object>>> ReadData(string sheetAndRange)
    {
        ArgumentNullException.ThrowIfNull(this.service);

        try
        {
            var request = this.service.Spreadsheets.Values.Get(this.googleSheetId, sheetAndRange);
            var response = await request.ExecuteAsync();
            var values = response.Values ?? new List<IList<object>>();

            Console.WriteLine($"Successfully read {values.Count} rows from the Google Sheet.");

            return values.Select(row => row.ToList()).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while reading the Google Sheet: {ex.Message}");
            throw;
        }
    }

    public async Task Open(string sheetId)
    {
        this.googleSheetId = sheetId ?? throw new ArgumentNullException(nameof(sheetId));
        if (!await Authenticate())
        {
            throw new ApplicationException("Authentication failed.");
        }

        this.service = CreateSheetsService();
    }

    public async Task<IEnumerable<string>> GetSheetNames()
    {
        ArgumentNullException.ThrowIfNull(this.service);

        // Create the request to get spreadsheet metadata.
        var request = this.service.Spreadsheets.Get(this.googleSheetId);

        // Use the "fields" parameter to ask for only the sheet properties.
        request.Fields = "sheets.properties.title,sheets.properties.sheetId";

        var spreadsheet = await request.ExecuteAsync();

        return spreadsheet.Sheets.Select(s => s.Properties.Title).ToList();
    }

    private async Task<bool> Authenticate()
    {
        if (this.credential is not null)
        {
            return true;
        }

        try
        {
            // Load the client secrets file for authentication.
            await using var stream = new FileStream(ClientSecretsFile, FileMode.Open, FileAccess.Read);
            // The DataStore stores your authentication token securely.
            this.credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                (await GoogleClientSecrets.FromStreamAsync(stream)).Secrets,
                Scopes,
                "user",
                CancellationToken.None,
                new FileDataStore("Sheets.Api.Store"));
            return true;
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine($"Error: The required file '{ClientSecretsFile}' was not found.");
            Console.WriteLine("Please download it from the Google Cloud Console and place it next to the application executable.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred during authentication: {ex.Message}");
        }

        return false;
    }

    private SheetsService CreateSheetsService()
    {
        if (this.service is not null)
        {
            return this.service;
        }

        return this.service = new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = this.credential,
            ApplicationName = Constants.ApplicationName
        });
    }
}
