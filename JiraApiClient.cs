using System.Text;
using System.Text.Json;

namespace BensJiraConsole;

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

    // New helper: fetch the last N closed sprints (returns sprint objects as JsonElement)
    public async Task<List<JsonElement>> GetLastClosedSprintsAsync(int boardId, int count = 5, int pageSizeFallback = 50)
    {
        if (count <= 0)
        {
            return new List<JsonElement>();
        }

        // 1) Try to fetch minimal data to discover total
        var minimal = await GetAgileBoardSprintsAsync(boardId, "closed", 0, 1);
        using var doc = JsonDocument.Parse(minimal);
        var root = doc.RootElement;

        // Helper to extract values array from a response string
        static List<JsonElement> ExtractValuesFromJson(string json)
        {
            using var d = JsonDocument.Parse(json);
            var r = d.RootElement;
            if (r.TryGetProperty("values", out var vals) && vals.ValueKind == JsonValueKind.Array)
            {
                return vals.EnumerateArray().ToList();
            }

            return new List<JsonElement>();
        }

        if (root.TryGetProperty("total", out var totalProp) && totalProp.ValueKind == JsonValueKind.Number && totalProp.TryGetInt32(out var total))
        {
            // We can compute the page that will contain the last `count` sprints
            var desiredStart = Math.Max(0, total - count);
            var page = await GetAgileBoardSprintsAsync(boardId, "closed", desiredStart, count);
            var values = ExtractValuesFromJson(page);
            // If the API returned fewer than requested, that's fine - return whatever we have (take last N)
            return values.Skip(Math.Max(0, values.Count - count)).ToList();
        }

        // If `total` is not present, fall back to paging until isLast==true, accumulate values
        var accumulated = new List<JsonElement>();
        var start = 0;
        var pageSize = pageSizeFallback > 0 ? pageSizeFallback : 50;

        while (true)
        {
            var pageJson = await GetAgileBoardSprintsAsync(boardId, "closed", start, pageSize);
            using var pDoc = JsonDocument.Parse(pageJson);
            var pRoot = pDoc.RootElement;

            if (pRoot.TryGetProperty("values", out var vals) && vals.ValueKind == JsonValueKind.Array)
            {
                foreach (var v in vals.EnumerateArray())
                {
                    accumulated.Add(v);
                }
            }

            // If the API provides isLast, use it. Otherwise, use length check
            var isLast = false;
            if (pRoot.TryGetProperty("isLast", out var isLastProp) && isLastProp.ValueKind == JsonValueKind.True)
            {
                isLast = true;
            }

            if (isLast)
            {
                break;
            }

            // If fewer values than pageSize returned, we've reached the end
            if (pRoot.TryGetProperty("values", out var v2) && v2.ValueKind == JsonValueKind.Array && v2.GetArrayLength() < pageSize)
            {
                break;
            }

            // advance
            start += pageSize;

            // safety: if we've accumulated more than count*10 items, stop to avoid huge loops
            if (accumulated.Count > count * 10 && start > 10000)
            {
                break;
            }
        }

        // return the last `count` items
        return accumulated.Skip(Math.Max(0, accumulated.Count - count)).ToList();
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
