﻿using System.Text.RegularExpressions;
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

    private UserCredential? credential;

    private string? googleSheetId;

    public async Task Open(string sheetId)
    {
        this.googleSheetId = sheetId;
        await Authenticate();
    }

    public string? CsvFilePathAndName { get; set; }

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

    public async Task ImportFile(string sheetAndRange, bool userMode = false)
    {
        if (CsvFilePathAndName is null)
        {
            throw new ArgumentException("CsvFilePathAndName has not been supplied to source data from.");
        }

        // Read the CSV data from the local file.
        IList<IList<object?>> values = new List<IList<object?>>();
        try
        {
            // Read all lines from the CSV file.
            var lines = await File.ReadAllLinesAsync(CsvFilePathAndName);
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
            Console.WriteLine($"Error: The CSV file '{CsvFilePathAndName}' was not found.");
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
        if (string.IsNullOrEmpty(this.googleSheetId))
        {
            throw new InvalidOperationException("Google Sheet ID is not set. Call Open(sheetId) first.");
        }

        if (!await Authenticate())
        {
            return;
        }

        using var service = new SheetsService(new BaseClientService.Initializer { HttpClientInitializer = this.credential, ApplicationName = Constants.ApplicationName });

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
