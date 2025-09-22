namespace BensJiraConsole;

public interface IWorkSheetReader
{
    Task Open(string sheetId);
    Task<IEnumerable<string>> GetSheetNames();
    Task<List<List<object>>> ReadData(string sheetAndRange);
}
