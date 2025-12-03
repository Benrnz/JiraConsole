namespace BensEngineeringMetrics;

public interface IWorkSheetReader
{
    Task<int> GetLastRowInColumnAsync(string sheetName, string columnLetter);
    Task<IEnumerable<string>> GetSheetNames();
    Task Open(string sheetId);
    Task<List<List<object>>> ReadData(string sheetAndRange);
}
