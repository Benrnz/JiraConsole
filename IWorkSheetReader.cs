namespace BensJiraConsole;

public interface IWorkSheetReader
{
    Task<IEnumerable<string>> GetSheetNames();
    Task<List<List<object>>> ReadData(string sheetAndRange);
}
