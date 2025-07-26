using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BensJiraConsole;
using CsvHelper;

public static class Program
{
    private const string BaseUrl = "https://javlnsupport.atlassian.net/rest/api/3/";
    private const string DefaultFolder = "C:\\Downloads\\JiraExports";
    private static readonly HttpClient Client = new();


    public static async Task Main(string[] args)
    {
        try
        {
            var issues = await ExecuteMode(args.Length > 0 ? args[0] : "NOT_SET");

            var fileName = $"{DefaultFolder}\\BensJiraConsole-{DateTime.Now:yyyyMMddHHmmss}.csv";
            WriteCsv(fileName, issues);
            Console.WriteLine("Export completed!");
            Console.WriteLine(Path.GetFullPath(fileName));
        }
        finally
        {
            Client.Dispose();
        }
    }

    private static async Task<List<JiraIssue>> ExecuteMode(string? mode)
    {
        switch (mode)
        {
            case null or "":
                return new List<JiraIssue>();
            case "1":
            case "JQL":
                return await ExportJqlQuery();
            case "2":
            case "PMPLAN":
                return await ExportPmPlanMapping();
            default:
                Console.WriteLine("Select Mode: (1)JQL or (2)PMPLAN: ");
                mode = Console.ReadLine();
                return await ExecuteMode(mode);
        }
    }

    private static async Task<List<JiraIssue>> ExportPmPlanMapping()
    {
        Console.WriteLine("Exporting a mapping of PMPlans to Stories.");
        var jqlPmPlans = "IssueType = Idea AND \"PM Customer[Checkboxes]\"= Envest ORDER BY Key";
        var pmPlans = (await GetIssuesFromJiraAsync(jqlPmPlans)).Select(i => i.Key);

        var allIssues = new List<JiraIssue>();
        foreach (var pmPlan in pmPlans)
        {
            var jql = $"parent in (linkedIssues(\"{pmPlan}\")) AND issuetype=Story ORDER BY key";
            var children = await GetIssuesFromJiraAsync(jql);
            Console.WriteLine($"Exported {children.Count} stories for {pmPlan}");
            children.ForEach(c => c.PmPlan = pmPlan);
            allIssues.AddRange(children);
        }

        return allIssues;
    }

    private static async Task<List<JiraIssue>> ExportJqlQuery()
    {
        Console.Write("Enter your JQL query: ");
        var jql = Console.ReadLine();

        var issues = await GetIssuesFromJiraAsync(jql);
        return issues;
    }

    // parent in (linkedIssues("PMPLAN-13")) AND issuetype=Story ORDER BY key
    private static async Task<List<JiraIssue>> GetIssuesFromJiraAsync(string jql)
    {
        var url = $"{BaseUrl}search?jql={Uri.EscapeDataString(jql)}";

        var email = Secrets.USERNAME;
        var token = Secrets.JIRA_TOKEN;
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{token}"));

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var requestBody = new
        {
            expand = "names",
            fields = new[]
            {
                "id",
                "summary",
                "status",
                "issuetype",
                "customfield_11934",
                "parent",
                "customfield_10004",
                "Original Estimate",
                "created"
            },
            fieldsByKeys = true,
            jql = jql,
            maxResults = 500,
            nextPageToken = "",
            reconcileIssues = new[] { 2154 }
        };
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await Client.PostAsync($"{BaseUrl}search", content);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        var jiraResponse = JsonSerializer.Deserialize<JiraResponse>(responseJson);

        var output = new List<JiraIssue>();
        foreach (var issue in jiraResponse.Issues)
        {
            output.Add(new JiraIssue(
                issue.Key,
                issue.Fields.Summary,
                issue.Fields.Status?.Name ?? "Unknown",
                issue.Fields.Assignee?.DisplayName ?? "Unassigned"
            ));
        }

        if (jiraResponse.Issues.Count == 500)
        {
            Console.WriteLine("WARNING! More than 500 issues found. Only the first 500 are exported.");
        }

        return output;
    }

    private static void WriteCsv(string path, List<JiraIssue> issues)
    {
        if (!Path.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
        }

        using var writer = new StreamWriter(path);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.WriteRecords(issues);
    }
}
