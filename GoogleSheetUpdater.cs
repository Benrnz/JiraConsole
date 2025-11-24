using System.Text.RegularExpressions;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Util.Store;
using File = System.IO.File;

namespace BensJiraConsole;

public class GoogleSheetUpdater : IWorkSheetUpdater
{
    private const string ClientSecretsFile = "client_secret_apps.googleusercontent.com.json";

    // The scopes required to access and modify Google Sheets.
    private static readonly string[] Scopes = [SheetsService.Scope.Spreadsheets];
    private static readonly Regex CsvParser = new(@",(?=(?:[^""]*""[^""]*"")*(?![^""]*""))");
    private readonly List<(string SheetName, int Column, string Format)> pendingApplyDateFormats = new();
    private readonly List<string> pendingClears = new();
    private readonly List<string> pendingDeleteSheetNames = new();

    // Batching queues
    private readonly List<Request> pendingSpreadsheetRequests = new();
    private readonly List<(ValueRange Range, bool UserMode)> pendingValueUpdates = new();

    private readonly Dictionary<string, int> sheetNamesToIds = new();

    private UserCredential? credential;

    private string? googleSheetId;

    public async Task Open(string sheetId)
    {
        this.googleSheetId = sheetId;
        await Authenticate();
    }

    public void AddSheet(string sheetName)
    {
        // Queue an AddSheet request; it will be sent on SubmitBatch().
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

        this.pendingSpreadsheetRequests.Add(addSheetRequest);
    }

    public void DeleteSheet(string sheetName)
    {
        // Queue delete by name; resolve SheetId in SubmitBatch().
        this.pendingDeleteSheetNames.Add(sheetName);
    }

    public async Task BoldCells(string sheetName, int startRow, int endRow, int startColumn, int endColumn)
    {
        var sheetId = await GetSheetIdByName(sheetName) ?? throw new ArgumentException($"Sheet {sheetName} does not exist.");
        var boldCellsRequest = new Request
        {
            RepeatCell = new RepeatCellRequest
            {
                Range = new GridRange
                {
                    SheetId = sheetId,
                    StartRowIndex = startRow,
                    EndRowIndex = endRow,
                    StartColumnIndex = startColumn,
                    EndColumnIndex = endColumn
                },
                Cell = new CellData
                {
                    UserEnteredFormat = new CellFormat
                    {
                        TextFormat = new TextFormat
                        {
                            Bold = true
                        }
                    }
                },
                Fields = "userEnteredFormat.textFormat.bold"
            }
        };

        this.pendingSpreadsheetRequests.Add(boldCellsRequest);
    }

    public async Task ClearRangeFormatting(string sheetName, int startRow, int endRow, int startColumn, int endColumn)
    {
        // Normalize sheet name (match ClearRange behavior)
        var normalized = sheetName.Contains("'") ? sheetName.Replace("'", "") : sheetName;

        // Resolve sheet id (throws if not found)
        var sheetId = await GetSheetIdByName(normalized) ?? throw new ArgumentException($"Sheet {sheetName} does not exist.");

        // Build and queue the RepeatCell request to clear formatting (reset userEnteredFormat)
        var clearFormatRequest = new Request
        {
            RepeatCell = new RepeatCellRequest
            {
                Range = new GridRange
                {
                    SheetId = sheetId,
                    StartRowIndex = startRow,
                    EndRowIndex = endRow,
                    StartColumnIndex = startColumn,
                    EndColumnIndex = endColumn
                },
                Cell = new CellData
                {
                    UserEnteredFormat = new CellFormat()
                },
                Fields = "userEnteredFormat"
            }
        };

        this.pendingSpreadsheetRequests.Add(clearFormatRequest);
    }

    public async Task<bool> DoesSheetExist(string spreadsheetId, string sheetName)
    {
        try
        {
            using var service = await InitiateService();

            // 1. Create a request to get the spreadsheet metadata.
            // We use a field mask to limit the response data to ONLY include sheet properties (title).
            // This makes the API call more performant by reducing the payload size.
            var getRequest = service.Spreadsheets.Get(spreadsheetId);
            getRequest.Fields = "sheets.properties.title"; // Field mask

            // 2. Execute the request.
            var spreadsheet = await getRequest.ExecuteAsync();

            // 3. Check if the Sheets collection exists and iterate through the sheets.
            if (spreadsheet.Sheets != null)
            {
                return spreadsheet.Sheets.Any(sheet => sheet.Properties.Title.Equals(sheetName));
            }

            // 4. If the loop completes without finding a match, the sheet does not exist.
            return false;
        }
        catch (Exception ex)
        {
            // Handle API errors, network issues, or invalid spreadsheet ID
            Console.WriteLine($"An error occurred: {ex.Message}");
            // Depending on your application's needs, you might want to rethrow the exception
            // or return false, assuming an error means the sheet couldn't be confirmed as existing.
            return false;
        }
    }

    public async Task HideColumn(string sheetName, int column)
    {
        var hideColumnRequest = new Request
        {
            UpdateDimensionProperties = new UpdateDimensionPropertiesRequest
            {
                Range = new DimensionRange
                {
                    SheetId = await GetSheetIdByName(sheetName),
                    Dimension = "COLUMNS", // Specify that we are modifying columns
                    StartIndex = column,
                    EndIndex = column + 1 // EndIndex is exclusive
                },
                Properties = new DimensionProperties
                {
                    HiddenByUser = true // Set the "hidden" property
                },
                Fields = "hiddenByUser" // Specify which property we are updating
            }
        };

        this.pendingSpreadsheetRequests.Add(hideColumnRequest);
    }

    public async Task ImportFile(string sheetAndRange, string csvFileName, bool userMode = false)
    {
        if (csvFileName is null)
        {
            throw new ArgumentNullException(nameof(csvFileName), "CsvFilePathAndName has not been supplied to source data from.");
        }

        // Read the CSV data from the local file.
        IList<IList<object?>> values = new List<IList<object?>>();
        try
        {
            // Read all lines from the CSV file.
            var lines = await File.ReadAllLinesAsync(csvFileName);
            foreach (var line in lines)
            {
                // Split preserving quoted strings with commas
                var parts = CsvParser.Split(line);
                var row = new List<object?>();
                foreach (var part in parts)
                {
                    row.Add(SetType(part.Trim()));
                }

                values.Add(row);
            }
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine($"Error: The CSV file '{csvFileName}' was not found.");
            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while reading the CSV file: {ex.Message}");
            return;
        }

        EditSheet(sheetAndRange, values, userMode);
    }

    public void EditSheet(string sheetAndRange, IList<IList<object?>> sourceData, bool userMode = false)
    {
        // Queue the value update; will be sent on SubmitBatch().
        var valueRange = new ValueRange
        {
            MajorDimension = "ROWS",
            Range = sheetAndRange,
            Values = sourceData
        };

        this.pendingValueUpdates.Add((valueRange, userMode));
    }

    public void ClearRange(string sheetName, string range = "A1:Z10000")
    {
        if (sheetName.Contains("'"))
        {
            sheetName = sheetName.Replace("'", "");
        }

        var sheetAndRange = $"'{sheetName}'!{range}";
        this.pendingClears.Add(sheetAndRange);
    }

    public void ApplyDateFormat(string sheetName, int column, string format)
    {
        // Queue apply date format by name; resolve SheetId in SubmitBatch().
        this.pendingApplyDateFormats.Add((sheetName, column, format));
    }

    public async Task SubmitBatch()
    {
        using var service = await InitiateService();

        await SendApplyClearRangeRequests(service);
        await SendApplyValueRangeRequests(service);
        await SendDeleteAddAndFormatRequests(service); // Formatting should be applied last, add delete don't matter and can be bundled into same service call.

        // Clear all queues after successful submission
        this.pendingSpreadsheetRequests.Clear();
        this.pendingDeleteSheetNames.Clear();
        this.pendingApplyDateFormats.Clear();
        this.pendingClears.Clear();
        this.pendingValueUpdates.Clear();
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

    /// <summary>
    ///     Gets the integer SheetId (gid) for a specific sheet tab name.
    /// </summary>
    private async Task<int?> GetSheetIdByName(string sheetName)
    {
        if (this.sheetNamesToIds.TryGetValue(sheetName, out var sheetId))
        {
            return sheetId;
        }

        using var service = await InitiateService();

        // Request only the sheet properties, not all the cell data
        var request = service.Spreadsheets.Get(this.googleSheetId);
        request.Fields = "sheets.properties";

        var spreadsheet = await request.ExecuteAsync();

        var sheet = spreadsheet.Sheets.FirstOrDefault(s => s.Properties.Title == sheetName);

        if (sheet is not null)
        {
            this.sheetNamesToIds.Add(sheetName, sheet.Properties.SheetId!.Value);
            return sheet.Properties.SheetId;
        }

        // Sheet name not found
        return null;
    }

    private async Task<SheetsService> InitiateService()
    {
        SheetsService? service = null;
        try
        {
            if (string.IsNullOrEmpty(this.googleSheetId))
            {
                throw new InvalidOperationException("Google Sheet ID is not set. Call Open(sheetId) first.");
            }

            if (!await Authenticate())
            {
                throw new ApplicationException("Google API Authentication failed.");
            }

            service = new SheetsService(new BaseClientService.Initializer { HttpClientInitializer = this.credential, ApplicationName = Constants.ApplicationName });
            return service;
        }
        catch
        {
            service?.Dispose();
            throw;
        }
    }

    private async Task SendApplyClearRangeRequests(SheetsService service)
    {
        // Batch clear ranges
        if (this.pendingClears.Any())
        {
            var batchClear = new BatchClearValuesRequest { Ranges = this.pendingClears.ToList() };
            await service.Spreadsheets.Values.BatchClear(batchClear, this.googleSheetId).ExecuteAsync();
        }
    }

    private async Task SendApplyValueRangeRequests(SheetsService service)
    {
        // Batch value updates, preserving the original order but group by input mode.
        for (var i = 0; i < 2; i++)
        {
            var modeRange = this.pendingValueUpdates
                .Where(p => p.UserMode == (i == 1))
                .Select(p => p.Range)
                .ToList();
            if (modeRange.Any())
            {
                var batchValues = new BatchUpdateValuesRequest
                {
                    ValueInputOption = i == 1 ? "USER_ENTERED" : "RAW",
                    Data = modeRange
                };
                await service.Spreadsheets.Values.BatchUpdate(batchValues, this.googleSheetId).ExecuteAsync();
            }
        }
    }

    private async Task SendDeleteAddAndFormatRequests(SheetsService service)
    {
        Spreadsheet? spreadsheet = null;
        if (this.pendingDeleteSheetNames.Any() || this.pendingApplyDateFormats.Any())
        {
            spreadsheet = await service.Spreadsheets.Get(this.googleSheetId).ExecuteAsync();
        }

        var requests = new List<Request>();

        // Resolve deletes first
        if (this.pendingDeleteSheetNames.Any() && spreadsheet is not null)
        {
            foreach (var name in this.pendingDeleteSheetNames)
            {
                var title = name.Trim('\'');
                var sheet = spreadsheet.Sheets.FirstOrDefault(s => s.Properties.Title == title);
                if (sheet?.Properties?.SheetId is { } sheetId)
                {
                    requests.Add(new Request
                    {
                        DeleteSheet = new DeleteSheetRequest { SheetId = sheetId }
                    });
                }
                else
                {
                    Console.WriteLine($"Warning: Sheet '{name}' not found to delete.");
                }
            }
        }

        // AddSheet and other prebuilt requests
        if (this.pendingSpreadsheetRequests.Any())
        {
            requests.AddRange(this.pendingSpreadsheetRequests);
        }

        // Apply date formats
        if (this.pendingApplyDateFormats.Any() && spreadsheet is not null)
        {
            foreach (var item in this.pendingApplyDateFormats)
            {
                var title = item.SheetName.Trim('\'');
                var sheet = spreadsheet.Sheets.FirstOrDefault(s => s.Properties.Title == title);
                if (sheet?.Properties?.SheetId is { } sheetId)
                {
                    requests.Add(new Request
                    {
                        RepeatCell = new RepeatCellRequest
                        {
                            Range = new GridRange
                            {
                                SheetId = sheetId,
                                StartColumnIndex = item.Column,
                                EndColumnIndex = item.Column + 1
                            },
                            Cell = new CellData
                            {
                                UserEnteredFormat = new CellFormat
                                {
                                    NumberFormat = new NumberFormat
                                    {
                                        Type = "DATE",
                                        Pattern = item.Format
                                    }
                                }
                            },
                            Fields = "userEnteredFormat.numberFormat"
                        }
                    });
                }
                else
                {
                    Console.WriteLine($"Warning: Sheet '{item.SheetName}' not found to apply date format.");
                }
            }
        }

        if (requests.Any())
        {
            var batchUpdate = new BatchUpdateSpreadsheetRequest { Requests = requests };
            await service.Spreadsheets.BatchUpdate(batchUpdate, this.googleSheetId).ExecuteAsync();
        }
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
