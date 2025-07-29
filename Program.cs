using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BensJiraConsole;
using CsvHelper;

public static class Program
{
    private const string DefaultFolder = "C:\\Downloads\\JiraExports";

    private static string[] PreferredFields { get; } =
    [
        "summary",
        "status",
        "issuetype",
        "customfield_11934", // Dev Time Spent
        "parent",
        "customfield_10004", // Story Points
        "created"
    ];


    public static async Task Main(string[] args)
    {
        var issues = await ExecuteMode(args.Length > 0 ? args[0] : "NOT_SET");

        var fileName = $"{DefaultFolder}\\BensJiraConsole-{DateTime.Now:yyyyMMddHHmmss}.csv";
        WriteCsv(fileName, issues);
        Console.WriteLine("Export completed!");
        Console.WriteLine(Path.GetFullPath(fileName));
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
        var client = new JiraApiClient();
        var jqlPmPlans = "IssueType = Idea AND \"PM Customer[Checkboxes]\"= Envest ORDER BY Key";
        var pmPlans = await PostSearchJiraIdeaAsync(jqlPmPlans, ["key", "summary", "customfield_11986", "customfield_12038", "customfield_12137"]);

        var allIssues = new List<JiraIssue>();
        foreach (var pmPlan in pmPlans)
        {
            var jql = $"parent in (linkedIssues(\"{pmPlan.Key}\")) AND issuetype=Story ORDER BY key";
            var children = await PostSearchJiraIssueAsync(jql);
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
        if (string.IsNullOrWhiteSpace(jql))
        {
            return new List<JiraIssue>();
        }

        var issues = await PostSearchJiraIssueAsync(jql);
        return issues;
    }



    private static async Task<List<JiraIssue>> PostSearchJiraIssueAsync(string jql, string[]? fields = null)
    {
        var client = new JiraApiClient();
        var responseJson = await client.PostSearchJqlAsync(jql, fields?? PreferredFields);
        var mapper = new JiraIssueMapper();
        return  mapper.MapToJiraIssue(responseJson);
    }

    private static async Task<List<JiraPmPlan>> PostSearchJiraIdeaAsync(string jql, string[]? fields = null)
    {
        var client = new JiraApiClient();
        var responseJson = await client.PostSearchJqlAsync(jql, fields?? PreferredFields);
        var mapper = new JiraIssueMapper();
        return  mapper.MapToPmPlan(responseJson);
    }

    private static void WriteCsv(string path, List<JiraIssue> issues)
    {
        if (!Path.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        }

        using var writer = new StreamWriter(path);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.WriteRecords(issues);
    }
}
