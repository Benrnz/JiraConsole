// See https://aka.ms/new-console-template for more information

using System.Net.Http.Headers;
using System.Globalization;
using System.Text.Json;
using BensJiraConsole;
using CsvHelper;

public static class Program
{
    private const string BASE_URL = "https://javlnsupport.atlassian.net/rest/api/3/";
    private const string DEFAULT_FOLDER = "C:\\Downloads\\JiraExports";
    private static HttpClient Client = new();
    
    
    public static async Task Main(string[] args)
    {
        try
        {
            var issues = await ExecuteMode(args.Length > 0 ? args[0] : "NOT_SET");

            var fileName = $"{DEFAULT_FOLDER}\\BensJiraConsole-{DateTime.Now:yyyyMMddHHmmss}.csv";
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
                return await ExportJQLQuery();
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
        Console.Write("Exporting a mapping of PMPlans to Stories.");
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

    private static async Task<List<JiraIssue>> ExportJQLQuery()
    {
        Console.Write("Enter your JQL query: ");
        string jql = Console.ReadLine();

        var issues = await GetIssuesFromJiraAsync(jql);
        return issues;
    }

    // parent in (linkedIssues("PMPLAN-13")) AND issuetype=Story ORDER BY key
    static async Task<List<JiraIssue>> GetIssuesFromJiraAsync(string jql)
    {
        var url = $"{BASE_URL}search?jql={Uri.EscapeDataString(jql)}";

        string email = Secrets.USERNAME;
        string token = Secrets.JIRA_TOKEN;
        string credentials = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{email}:{token}"));

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var response = await Client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var jiraResponse = JsonSerializer.Deserialize<JiraResponse>(json);


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

        return output;
    }

    static void WriteCsv(string path, List<JiraIssue> issues)
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