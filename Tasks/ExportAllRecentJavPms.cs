using BensEngineeringMetrics.Jira;

namespace BensEngineeringMetrics.Tasks;

public class ExportAllRecentJavPms(IJiraQueryRunner runner, ICsvExporter exporter) : IEngineeringMetricsTask
{
    private const string KeyString = "ALLTICKETS";

    /// <summary> Fields to include in the export: </summary>
    private static readonly IFieldMapping[] Fields =
    [
        JiraFields.Summary,
        JiraFields.Status,
        JiraFields.IssueType,
        JiraFields.DevTimeSpent,
        JiraFields.ParentKey,
        JiraFields.StoryPoints,
        JiraFields.Created,
        JiraFields.AssigneeDisplay,
        JiraFields.BugType,
        JiraFields.CustomersMultiSelect,
        JiraFields.Category,
        JiraFields.Resolution,
        JiraFields.OriginalEstimate,
        JiraFields.FlagCount,
        JiraFields.Severity,
        JiraFields.Sprint,
        JiraFields.Priority,
        JiraFields.Resolved,
        JiraFields.Team
    ];

    public string Key => KeyString;
    public string Description => "Export JAVPM tickets from the last 18 months. Or optionally, specify a filter as an argument and export any tickets from any project.";

    public async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine($"{Key} - {Description}");
        var jql = args.Length > 1
            ? $"filter = {args[1]}"
            : "project=JAVPM AND created > -540d ORDER BY created"; //540 days = 18 months

        Console.WriteLine(jql);
        var issues = await runner.SearchJiraIssuesWithJqlAsync(jql, Fields);
        Console.WriteLine($"{issues.Count} issues fetched.");

        exporter.SetFileNameMode(FileNameMode.Hint, Key);
        exporter.Export(issues);
    }
}
