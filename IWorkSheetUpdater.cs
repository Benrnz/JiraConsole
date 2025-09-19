namespace BensJiraConsole;

public interface IWorkSheetUpdater
{
    string? CsvFilePathAndName { get; set; }
    bool QuoteStrings { get; set; }
    Task AddSheet(string sheetName);

    Task ApplyDateFormat(string sheetName, int column, string format);

    /// <summary>
    ///     Clear the sheet / range values.
    /// </summary>
    Task ClearSheet(string sheetName, string range = "A1:Z10000");

    Task DeleteSheet(string sheetName);

    /// <summary>
    ///     Edit a sheet and insert data provided in the CSV file.
    /// </summary>
    /// <param name="sheetAndRange">'Sheet1!A1'</param>
    /// <param name="userMode">Defaults to false.  If true, data is entered and interpreted by the workbook as if entered by the user.</param>
    Task EditSheet(string sheetAndRange, bool userMode = false);

    /// <summary>
    ///     Edit a sheet and insert data provided by <paramref name="sourceData" />.
    /// </summary>
    /// <param name="sheetAndRange">'Sheet1!A1'</param>
    /// <param name="sourceData">data to be inserted into the sheet</param>
    /// <param name="userMode">Defaults to false.  If true, data is entered and interpreted by the workbook as if entered by the user.</param>
    Task EditSheet(string sheetAndRange, IList<IList<object?>> sourceData, bool userMode = false);
}
