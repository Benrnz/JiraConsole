namespace BensJiraConsole;

public interface IJiraExportTask
{
    string Key { get; }
    string Description { get; }
    Task ExecuteAsync(string[] fields);
}
