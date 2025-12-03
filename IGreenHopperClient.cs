using System.Text.Json.Nodes;

namespace BensEngineeringMetrics;

public record SprintTicket(string Key, string Status, string IssueType);

public interface IGreenHopperClient
{
    /// <summary>
    ///     Retrieves the raw sprint report JSON from Jira GreenHopper and parses it into a JSON DOM object.
    /// </summary>
    /// <param name="boardId">The Rapid View (sprint board) ID.</param>
    /// <param name="sprintId">The sprint ID.</param>
    /// <returns>A JSON node representing the full JSON payload (all properties preserved). Returned as dynamic for compatibility.</returns>
    Task<JsonNode?> GetSprintReportAsync(int boardId, int sprintId);

    /// <summary>
    ///     Retrieves all sprint tickets (completed and not completed) for a given board and sprint.
    ///     Completed means it was completed during the sprint, not its current status at the time of this query.
    /// </summary>
    /// <param name="boardId">The Rapid View (sprint board) ID.</param>
    /// <param name="sprintId">The sprint ID.</param>
    /// <returns>A list of sprint tickets containing Key, Status, and IssueType.</returns>
    Task<IReadOnlyList<SprintTicket>> GetSprintTicketsAsync(int boardId, int sprintId);
}
