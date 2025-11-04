using System.Text;
using System.Text.Json;

namespace BensJiraConsole;

public class JiraApiClient
{
    private const string BaseApi3Url = "https://javlnsupport.atlassian.net/rest/api/3/";
    private const string BaseAgileUrl = "https://javlnsupport.atlassian.net/rest/agile/1.0/";

    public async Task<string> GetAgileBoardActiveSprintAsync(int boardId)
    {
        return await GetAgileBoardByStateAsync(boardId, "active");
    }

    public async Task<string> GetAgileBoardClosedSprintsAsync(int boardId)
    {
        return await GetAgileBoardByStateAsync(boardId, "closed");
    }

    public async Task<string> GetAgileBoardFutureSprintsAsync(int boardId)
    {
        return await GetAgileBoardByStateAsync(boardId, "future");
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

    private async Task<string> GetAgileBoardByStateAsync(int boardId, string state)
    {
        var response = await App.Http.GetAsync($"{BaseAgileUrl}/board/{boardId}/sprint?state={state}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
