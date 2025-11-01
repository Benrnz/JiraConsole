namespace BensJiraConsole;

public interface IJiraQueryRunner
{
    Task<AgileSprint?> GetCurrentSprint(int boardId);
    Task<IReadOnlyList<dynamic>> SearchJiraIssuesWithJqlAsync(string jql, IFieldMapping[] fields);
}
