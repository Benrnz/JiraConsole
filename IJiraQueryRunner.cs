namespace BensJiraConsole;

public interface IJiraQueryRunner
{
    Task<AgileSprint?> GetCurrentSprintForBoard(int boardId);
    Task<AgileSprint?> GetSprintById(int sprintId);
    Task<IReadOnlyList<dynamic>> SearchJiraIssuesWithJqlAsync(string jql, IFieldMapping[] fields);
}
