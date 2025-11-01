using System.Text;
using System.Text.Json;

namespace BensJiraConsole;

public class JiraApiClient
{
    private const string BaseUrl = "https://javlnsupport.atlassian.net/rest/api/3/";

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

        var response = await App.Http.PostAsync($"{BaseUrl}search/jql", content);
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
}
