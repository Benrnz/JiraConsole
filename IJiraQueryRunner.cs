using BensJiraConsole.Jira;

namespace BensJiraConsole;

public interface IJiraQueryRunner
{
    Task<AgileSprint?> GetCurrentSprintForBoard(int boardId);
    Task<AgileSprint?> GetSprintById(int sprintId);
    Task<IReadOnlyList<dynamic>> SearchJiraIssuesWithJqlAsync(string jql, IFieldMapping[] fields);

    /// <summary>
    ///     Gets all sprint numbers for a given board ID.
    /// </summary>
    /// <param name="boardId">The Jira Agile board ID</param>
    /// <returns>A list of sprint numbers (IDs)</returns>
    Task<IReadOnlyList<AgileSprint>> GetAllSprints(int boardId);
}
