namespace BensJiraConsole;

public interface IWorkSheetUpdater
{
    string? CsvFilePathAndName { get; set; }
    bool QuoteStrings { get; set; }
    Task AddSheet(string sheetName);

    Task ApplyDateFormat(string sheetName, int column, string format);
    Task ClearSheet(string sheetName);
    Task DeleteSheet(string sheetName);
    Task EditSheet(string sheetAndRange);
}
