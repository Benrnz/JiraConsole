using System.Text.Json.Nodes;

namespace BensJiraConsole;

public class GreenHopperDynamicRunner : IGreenHopperRunner
{
    private readonly JiraGreenHopperClient greenhopperClient = new();

    /// <summary>
    /// Retrieves the raw sprint report JSON from Jira GreenHopper and parses it into a JSON DOM object.
    /// </summary>
    /// <param name="boardId">The Rapid View (board) ID.</param>
    /// <param name="sprintId">The sprint ID.</param>
    /// <returns>A JSON node representing the full JSON payload (all properties preserved). Returned as dynamic for compatibility.</returns>
    public async Task<JsonNode?> GetSprintReportAsync(int boardId, int sprintId)
    {
        var responseJson = await this.greenhopperClient.GetSprintReportAsync(boardId, sprintId);
        // Parse into System.Text.Json DOM to retain all properties flexibly without Newtonsoft.Json
        return JsonNode.Parse(responseJson);
    }
}
