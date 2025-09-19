using System.Text.RegularExpressions;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Util.Store;
using File = System.IO.File;

namespace BensJiraConsole;

public class GoogleSheetUpdater(string googleSheetId) : IWorkSheetUpdater
{
    private const string ClientSecretsFile = "client_secret_apps.googleusercontent.com.json";

    // The scopes required to access and modify Google Sheets.
    private static readonly string[] Scopes = [SheetsService.Scope.Spreadsheets];
    private static readonly Regex CsvParser = new(@",(?=(?:[^""]*""[^""]*"")*(?![^""]*""))");

    private readonly string googleSheetId = googleSheetId ?? throw new ArgumentNullException(nameof(googleSheetId));

    private UserCredential? credential;

    private SheetsService? service;

    public string? CsvFilePathAndName { get; set; }

    public bool QuoteStrings { get; set; } = false;

    public async Task AddSheet(string sheetName)
    {
        if (!await Authenticate())
        {
            return;
        }

        this.service = CreateSheetsService();

        try
        {
            var addSheetRequest = new Request
            {
                AddSheet = new AddSheetRequest
                {
                    Properties = new SheetProperties
                    {
                        Title = sheetName
                    }
                }
            };

            var batchUpdateRequest = new BatchUpdateSpreadsheetRequest
            {
                Requests = new List<Request> { addSheetRequest }
            };

            await this.service.Spreadsheets.BatchUpdate(batchUpdateRequest, this.googleSheetId).ExecuteAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while adding the sheet: {ex.Message}");
            throw;
        }
    }

    public async Task DeleteSheet(string sheetName)
    {
        if (!await Authenticate())
        {
            return;
        }

        this.service = CreateSheetsService();

        try
        {
            // First, get the spreadsheet to find the sheet ID
            var spreadsheet = await this.service.Spreadsheets.Get(this.googleSheetId).ExecuteAsync();
            var sheet = spreadsheet.Sheets.FirstOrDefault(s => s.Properties.Title == sheetName);

            if (sheet == null)
            {
                throw new Exception($"Sheet '{sheetName}' not found");
            }

            var deleteRequest = new Request
            {
                DeleteSheet = new DeleteSheetRequest
                {
                    SheetId = sheet.Properties.SheetId
                }
            };

            var batchUpdateRequest = new BatchUpdateSpreadsheetRequest
            {
                Requests = new List<Request> { deleteRequest }
            };

            await this.service.Spreadsheets.BatchUpdate(batchUpdateRequest, this.googleSheetId).ExecuteAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while deleting the sheet (in order to clear the data). Message: {ex.Message}");
            throw;
        }
    }

    public async Task EditSheet(string sheetAndRange, bool userMode = false)
    {
        if (CsvFilePathAndName is null)
        {
            throw new ArgumentException("CsvFilePathAndName has not been supplied to source data from.");
        }

        // Read the CSV data from the local file.
        IList<IList<object>> values = new List<IList<object>>();
        try
        {
            // Read all lines from the CSV file.
            var lines = await File.ReadAllLinesAsync(CsvFilePathAndName);
            foreach (var line in lines)
            {
                // Split preserving quoted strings with commas
                var parts = CsvParser.Split(line);
                var row = new List<object>();
                foreach (var part in parts)
                {
                    row.Add(SetType(part.Trim()));
                }

                values.Add(row);
            }
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine($"Error: The CSV file '{CsvFilePathAndName}' was not found.");
            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while reading the CSV file: {ex.Message}");
            return;
        }

        if (!await Authenticate())
        {
            return;
        }

        // Create the Google Sheets service client.
        this.service = CreateSheetsService();

        await EditSheet(sheetAndRange, values, userMode);
    }

    public async Task EditSheet(string sheetAndRange, IList<IList<object>> sourceData, bool userMode = false)
    {
        if (!await Authenticate())
        {
            return;
        }

        // Create the Google Sheets service client.
        this.service = CreateSheetsService();

        // Define the data to be updated in the Google Sheet.
        var valueRange = new ValueRange
        {
            MajorDimension = "ROWS",
            Values = sourceData
        };

        try
        {
            // Create the update request.
            var updateRequest = this.service.Spreadsheets.Values.Update(valueRange, this.googleSheetId, sheetAndRange);
            updateRequest.ValueInputOption =
                userMode ? SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED : SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;

            // Execute the request to update the Google Sheet.
            var response = await updateRequest.ExecuteAsync();

            Console.WriteLine($"\nSuccessfully updated {response.UpdatedCells} cells in https://docs.google.com/spreadsheets/d/{this.googleSheetId}/");
            Console.WriteLine("Import complete. Press any key to exit.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred during the API call: {ex.Message}");
        }
    }

    public async Task ClearSheet(string sheetName, string range = "A1:Z10000")
    {
        if (!await Authenticate())
        {
            return;
        }

        // Create the Google Sheets service client.
        this.service = CreateSheetsService();

        var sheetAndrange = $"'{sheetName}'!{range}"; // Adjust range as needed
        var requestBody = new ClearValuesRequest();

        var request = this.service.Spreadsheets.Values.Clear(requestBody, this.googleSheetId, sheetAndrange);
        await request.ExecuteAsync();
    }

    public async Task ApplyDateFormat(string sheetName, int column, string format)
    {
        if (!await Authenticate())
        {
            return;
        }

        this.service = CreateSheetsService();

        try
        {
            // First, get the spreadsheet to find the sheet ID
            var spreadsheet = await this.service.Spreadsheets.Get(this.googleSheetId).ExecuteAsync();
            var sheet = spreadsheet.Sheets.FirstOrDefault(s => s.Properties.Title == sheetName);

            if (sheet == null)
            {
                throw new Exception($"Sheet '{sheetName}' not found");
            }

            // Create the request to format the cells.
            var repeatCellRequest = new Request
            {
                RepeatCell = new RepeatCellRequest
                {
                    // Define the range to apply the format to.
                    // This example targets all of Column A on the first sheet (sheetId: 0).
                    Range = new GridRange
                    {
                        SheetId = sheet.Properties.SheetId, // 0 is the ID of the very first sheet
                        StartColumnIndex = column, // 0 is Column A
                        EndColumnIndex = column + 1 // The range is exclusive, so this stops before Column B
                    },
                    // Define the format to apply.
                    Cell = new CellData
                    {
                        UserEnteredFormat = new CellFormat
                        {
                            NumberFormat = new NumberFormat
                            {
                                Type = "DATE",
                                Pattern = format // Your desired custom date format
                            }
                        }
                    },
                    // Specify that we are only updating the number format.
                    Fields = "userEnteredFormat.numberFormat"
                }
            };

            var batchUpdateRequest = new BatchUpdateSpreadsheetRequest
            {
                Requests = new List<Request> { repeatCellRequest }
            };

            await this.service.Spreadsheets.BatchUpdate(batchUpdateRequest, this.googleSheetId).ExecuteAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while editing the sheet. Message: {ex.Message}");
            throw;
        }
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

    private object SetType(string? value)
    {
        if (value == null)
        {
            return string.Empty;
        }

        if (int.TryParse(value, out var intValue))
        {
            return intValue;
        }

        if (double.TryParse(value, out var doubleValue))
        {
            return doubleValue;
        }

        // Assume string
        if (QuoteStrings)
        {
            return value;
        }

        // Strip quotes if present
        if (value.StartsWith("\""))
        {
            value = value.Remove(0, 1);
        }

        if (value.EndsWith("\""))
        {
            value = value.Remove(value.Length - 1, 1);
        }

        return value;
    }
}
