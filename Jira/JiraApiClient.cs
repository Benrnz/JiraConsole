using System.Text;
using System.Text.Json;

namespace BensJiraConsole.Jira;

public class JiraApiClient
{
    private const string BaseApi3Url = "https://javlnsupport.atlassian.net/rest/api/3/";
    private const string BaseAgileUrl = "https://javlnsupport.atlassian.net/rest/agile/1.0/";

    public async Task<string> GetAgileBoardActiveSprintAsync(int boardId)
    {
        return await GetAgileBoardSprintsAsync(boardId, "active");
    }

    public async Task<string> GetAgileBoardAllSprintsAsync(int boardId, int? startAt = null, int? maxResults = null)
    {
        var sb = new StringBuilder($"{BaseAgileUrl}board/{boardId}/sprint");

        // Build query parameters cleanly using a list
        var queryParts = new List<string>();

        if (startAt.HasValue)
        {
            queryParts.Add($"startAt={startAt.Value}");
        }

        if (maxResults.HasValue)
        {
            queryParts.Add($"maxResults={maxResults.Value}");
        }

        if (queryParts.Any())
        {
            sb.Append('?').Append(string.Join('&', queryParts));
        }

        var response = await App.Http.GetAsync(sb.ToString());
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetAgileBoardSprintByIdAsync(int sprintId)
    {
        var response = await App.Http.GetAsync($"{BaseAgileUrl}sprint/{sprintId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> PostSearchJqlAsync(string jql, string[] fields, string? nextPageToken = null)
    {
        var requestBody = new
        {
            expand = "names",
            fields,
            jql,
            maxResults = 500,
            nextPageToken
        };
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await App.Http.PostAsync($"{BaseApi3Url}search/jql", content);
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine("ERROR!");
            Console.WriteLine(response.StatusCode);
            Console.WriteLine(response.ReasonPhrase);
            Console.WriteLine(json);
        }

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        return responseJson;
    }

    // New private helper: get sprints for a specific state with paging
    private async Task<string> GetAgileBoardSprintsAsync(int boardId, string state, int? startAt = null, int? maxResults = null)
    {
        var sb = new StringBuilder($"{BaseAgileUrl}board/{boardId}/sprint");
        var queryParts = new List<string>
        {
            $"state={Uri.EscapeDataString(state)}"
        };

        if (startAt.HasValue)
        {
            queryParts.Add($"startAt={startAt.Value}");
        }

        if (maxResults.HasValue)
        {
            queryParts.Add($"maxResults={maxResults.Value}");
        }

        if (queryParts.Any())
        {
            sb.Append('?').Append(string.Join('&', queryParts));
        }

        var response = await App.Http.GetAsync(sb.ToString());
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
