using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Util.Store;

namespace BensJiraConsole;

public class GoogleSheetUpdater
{
    private readonly string csvFilePathAndName;

    // The scopes required to access and modify Google Sheets.
    private static readonly string[] Scopes = [SheetsService.Scope.Spreadsheets];

    private const string GoogleSheetId = "16bZeQEPobWcpsD8w7cI2ftdSoT1xWJS8eu41JTJP-oI";
    private const string SheetAndRange = "'BUG_STATS-Severities.CSV'!A1";
    private const string ClientSecretsFile = "client_secret_apps.googleusercontent.com.json";

    public GoogleSheetUpdater(string csvFilePathAndName)
    {
        this.csvFilePathAndName = csvFilePathAndName ?? throw new ArgumentNullException(nameof(csvFilePathAndName));
    }

    public async Task EditGoogleSheet()
    {
        Console.WriteLine("Starting Google Sheet data import...");

        UserCredential credential;

        try
        {
            // Load the client secrets file for authentication.
            await using var stream = new FileStream(ClientSecretsFile, FileMode.Open, FileAccess.Read);
            // The DataStore stores your authentication token securely.
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
            Console.WriteLine("Please download it from the Google Cloud Console and place it next to the application executable.");
            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred during authentication: {ex.Message}");
            return;
        }

        Console.WriteLine("Authentication successful. Building Sheets service...");

        // Create the Google Sheets service client.
        var service = new SheetsService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = Constants.ApplicationName,
        });

        // Read the CSV data from the local file.
        IList<IList<object>> values = new List<IList<object>>();
        try
        {
            // Read all lines from the CSV file.
            var lines = await File.ReadAllLinesAsync(this.csvFilePathAndName);
            foreach (var line in lines)
            {
                // Split each line by comma and add to the list of values.
                var parts = line.Split(',');
                var row = new List<object>();
                foreach (var part in parts)
                {
                    row.Add(part.Trim());
                }
                values.Add(row);
            }
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine($"Error: The CSV file '{this.csvFilePathAndName}' was not found.");
            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while reading the CSV file: {ex.Message}");
            return;
        }

        Console.WriteLine($"Successfully read {values.Count} rows from {this.csvFilePathAndName}.");

        // Define the data to be updated in the Google Sheet.
        var valueRange = new ValueRange
        {
            MajorDimension = "ROWS",
            Values = values
        };

        try
        {
            // Create the update request.
            var updateRequest = service.Spreadsheets.Values.Update(valueRange, GoogleSheetId, SheetAndRange);
            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;

            // Execute the request to update the Google Sheet.
            var response = await updateRequest.ExecuteAsync();

            Console.WriteLine($"\nSuccessfully updated {response.UpdatedCells} cells in Google Sheet ID: {GoogleSheetId}");
            Console.WriteLine("Import complete. Press any key to exit.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred during the API call: {ex.Message}");
        }
    }
}
