namespace BensJiraConsole;

public interface IWorkSheetUpdater
{
    string CsvFilePathAndName { get; set; }
    bool QuoteStrings { get; set; }
    Task AddSheet(string sheetName);
    Task ClearSheet(string sheetName);
    Task DeleteSheet(string sheetName);
    Task EditGoogleSheet(string sheetAndRange);
}
