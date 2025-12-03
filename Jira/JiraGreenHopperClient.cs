using System.Text.Json.Nodes;

namespace BensEngineeringMetrics.Jira;

public class JiraGreenHopperClient : IGreenHopperClient
{
    private const string BaseUrl = "https://javlnsupport.atlassian.net/rest/greenhopper/1.0/";

    public async Task<JsonNode?> GetSprintReportAsync(int sprintBoardId, int sprintId)
    {
        var url = $"{BaseUrl}rapid/charts/sprintreport?rapidViewId={sprintBoardId}&sprintId={sprintId}";

        var response = await App.HttpJira.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine("ERROR calling Greenhopper sprint report API!");
            Console.WriteLine(response.StatusCode);
            Console.WriteLine(response.ReasonPhrase);
            Console.WriteLine(url);
        }

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        // Parse into System.Text.Json DOM to retain all properties flexibly without Newtonsoft.Json
        return JsonNode.Parse(responseJson);
    }

    public async Task<IReadOnlyList<SprintTicket>> GetSprintTicketsAsync(int boardId, int sprintId)
    {
        var sprintReport = await GetSprintReportAsync(boardId, sprintId);
        if (sprintReport == null)
        {
            return [];
        }

        var contents = sprintReport["contents"];
        if (contents == null)
        {
            return [];
        }

        var tickets = new List<SprintTicket>();

        // Extract completed issues
        var completedIssues = contents["completedIssues"]?.AsArray();
        if (completedIssues != null)
        {
            foreach (var issue in completedIssues)
            {
                var ticket = ExtractSprintTicket(issue);
                if (ticket != null)
                {
                    tickets.Add(ticket);
                }
            }
        }

        // Extract not completed issues
        var notCompletedIssues = contents["issuesNotCompletedInCurrentSprint"]?.AsArray();
        if (notCompletedIssues != null)
        {
            foreach (var issue in notCompletedIssues)
            {
                var ticket = ExtractSprintTicket(issue);
                if (ticket != null)
                {
                    tickets.Add(ticket);
                }
            }
        }

        return tickets;
    }

    private static SprintTicket? ExtractSprintTicket(JsonNode? issue)
    {
        if (issue == null)
        {
            return null;
        }

        var key = issue["key"]?.GetValue<string>();
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        var status = issue["statusName"]?.GetValue<string>() ??
                     issue["status"]?["name"]?.GetValue<string>() ??
                     string.Empty;

        var issueType = issue["typeName"]?.GetValue<string>() ??
                        issue["type"]?.GetValue<string>() ??
                        issue["issuetype"]?["name"]?.GetValue<string>() ??
                        string.Empty;

        return new SprintTicket(key, status, issueType);
    }
}
