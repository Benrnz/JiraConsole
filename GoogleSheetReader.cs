using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Util.Store;

namespace BensJiraConsole;

public class GoogleSheetReader(string googleSheetId)
{
    private readonly string googleSheetId = googleSheetId ?? throw new ArgumentNullException(nameof(googleSheetId));
    private static readonly string[] Scopes = [SheetsService.Scope.Spreadsheets];
    private const string ClientSecretsFile = "client_secret_apps.googleusercontent.com.json";

    public async Task<List<List<object>>> ReadData(string sheetAndRange)
    {
        UserCredential credential;

        try
        {
            await using var stream = new FileStream(ClientSecretsFile, FileMode.Open, FileAccess.Read);
            credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                (await GoogleClientSecrets.FromStreamAsync(stream)).Secrets,
                Scopes,
                "user",
                CancellationToken.None,
                new FileDataStore("Sheets.Api.Store"));
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine($"Error: The required file '{ClientSecretsFile}' was not found.");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred during authentication: {ex.Message}");
            throw;
        }

        var service = new SheetsService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = Constants.ApplicationName
        });

        try
        {
            var request = service.Spreadsheets.Values.Get(this.googleSheetId, sheetAndRange);
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
}
