namespace BensJiraConsole.Tasks;

public class ExportAllRecentJavPms : IJiraExportTask
{
    /// <summary> Fields to include in the export: </summary>
    private static  readonly FieldMapping[] Fields =
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

    public string Key => "JAVPMs";
    public string Description => "Export all JAVPM tickets from the last 18 months.";

    public async Task ExecuteAsync(string[] fields)
    {
        Console.WriteLine(Description);
        var jql = "project=JAVPM AND created > -540d ORDER BY created"; //540 days = 18 months
        Console.WriteLine(jql);
        var runner = new JiraQueryDynamicRunner();
        var issues = await runner.SearchJiraIssuesWithJqlAsync(jql, Fields);
        Console.WriteLine($"{issues.Count} issues fetched.");

        var exporter = new SimpleCsvExporter(Key);
        exporter.Export(issues);
    }
}
