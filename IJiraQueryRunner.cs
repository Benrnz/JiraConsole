namespace BensJiraConsole;

public interface IJiraQueryRunner
{
    Task<IReadOnlyList<dynamic>> SearchJiraIssuesWithJqlAsync(string jql, IFieldMapping[] fields);
}
