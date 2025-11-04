using System.Text.Json.Nodes;

namespace BensJiraConsole;

public interface IGreenHopperClient
{
    /// <summary>
    ///     Retrieves the raw sprint report JSON from Jira GreenHopper and parses it into a JSON DOM object.
    /// </summary>
    /// <param name="boardId">The Rapid View (sprint board) ID.</param>
    /// <param name="sprintId">The sprint ID.</param>
    /// <returns>A JSON node representing the full JSON payload (all properties preserved). Returned as dynamic for compatibility.</returns>
    Task<JsonNode?> GetSprintReportAsync(int boardId, int sprintId);
}
