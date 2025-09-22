namespace BensJiraConsole.Tasks;

public class ExportAllRecentJavPms : IJiraExportTask
{
    private const string KeyString = "ALLJAVPM";

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

    private readonly ICsvExporter exporter = new SimpleCsvExporter(KeyString);
    private readonly IJiraQueryRunner runner = new JiraQueryDynamicRunner();

    public string Key => KeyString;
    public string Description => "Export _all_JAVPM_ tickets from the last 18 months.";

    public async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine(Description);
        var jql = "project=JAVPM AND created > -540d ORDER BY created"; //540 days = 18 months
        Console.WriteLine(jql);
        var issues = await this.runner.SearchJiraIssuesWithJqlAsync(jql, Fields);
        Console.WriteLine($"{issues.Count} issues fetched.");

        this.exporter.Export(issues);
    }
}
