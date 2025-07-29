namespace BensJiraConsole;

public interface IJiraExportTask
{
    string Key { get; }
    string Description { get; }
    Task<List<JiraIssue>> ExecuteAsync(string[] fields);
}
