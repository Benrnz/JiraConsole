namespace BensJiraConsole;

public interface IWorkSheetUpdater
{
    string? CsvFilePathAndName { get; set; }
    bool QuoteStrings { get; set; }
    Task AddSheet(string sheetName);

    Task ApplyDateFormat(string sheetName, int column, string format);
    Task ClearSheet(string sheetName);
    Task DeleteSheet(string sheetName);

    /// <summary>
    ///     Edit a sheet and insert data provided in the CSV file.
    /// </summary>
    /// <param name="sheetAndRange">'Sheet1!A1'</param>
    /// <param name="userMode">Defaults to false.  If true, data is entered and interpretted by the workbook as if entered by the user.</param>
    Task EditSheet(string sheetAndRange, bool userMode = false);
}
