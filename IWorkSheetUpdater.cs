namespace BensJiraConsole;

public interface IWorkSheetUpdater
{
    string? CsvFilePathAndName { get; set; }

    void AddSheet(string sheetName);

    void ApplyDateFormat(string sheetName, int column, string format);

    /// <summary>
    ///     Clear the sheet / range values.
    /// </summary>
    void ClearRange(string sheetName, string range = "A1:Z10000");

    void DeleteSheet(string sheetName);

    /// <summary>
    ///     Edit a sheet and insert data provided by <paramref name="sourceData" />.
    /// </summary>
    /// <param name="sheetAndRange">'Sheet1!A1'</param>
    /// <param name="sourceData">data to be inserted into the sheet</param>
    /// <param name="userMode">Defaults to false.  If true, data is entered and interpreted by the workbook as if entered by the user.</param>
    void EditSheet(string sheetAndRange, IList<IList<object?>> sourceData, bool userMode = false);

    /// <summary>
    ///     Edit a sheet and insert data provided in the CSV file.
    /// </summary>
    /// <param name="sheetAndRange">'Sheet1!A1'</param>
    /// <param name="userMode">Defaults to false.  If true, data is entered and interpreted by the workbook as if entered by the user.</param>
    Task ImportFile(string sheetAndRange, bool userMode = false);

    Task Open(string sheetId);

    /// <summary>
    ///     Submit all queued changes to Google Sheets in as few requests as possible.
    ///     Sends a single spreadsheet `BatchUpdateSpreadsheetRequest` for structural/formatting changes
    ///     and batches value updates/clears using the Values API.
    /// </summary>
    Task SubmitBatch();
}
