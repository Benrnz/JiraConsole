using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace BensJiraConsole;

public class JiraApiClient
{
    private static readonly HttpClient Client = new();
    private const string BaseUrl = "https://javlnsupport.atlassian.net/rest/api/3/";

    public JiraApiClient()
    {
        var email = Secrets.Username;
        var token = Secrets.JiraToken;
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{token}"));

        if (Client.DefaultRequestHeaders.Authorization == null)
        {
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }
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

        var response = await Client.PostAsync($"{BaseUrl}search/jql", content);
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

    // private static async Task<List<JiraIssue>> GetSearchJiraAsync(string jql)
    // {
    //     var url = $"{BaseUrl}search?jql={Uri.EscapeDataString(jql)}";
    //
    //     var response = await Client.GetAsync(url);
    //     response.EnsureSuccessStatusCode();
    //
    //     var json = await response.Content.ReadAsStringAsync();
    //     var jiraResponse = JsonSerializer.Deserialize<JiraResponseDto>(json);
    //
    //     var output = new List<JiraIssue>();
    //     foreach (var issue in jiraResponse.Issues)
    //     {
    //         output.Add(new JiraIssue(
    //             issue.Key,
    //             issue.Fields.Summary,
    //             issue.Fields.Status?.Name ?? "Unknown",
    //             issue.Fields.Assignee?.DisplayName ?? "Unassigned"
    //         ));
    //     }
    //
    //     return output;
    // }
}
