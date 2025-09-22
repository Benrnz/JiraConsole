namespace BensJiraConsole.Tasks;

// ReSharper disable once UnusedType.Global
public class ExportJqlQueryTask : IJiraExportTask
{
    private const string KeyString = "JQL";

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

    public string Description => "Export issues matching a _JQL_ query";


    public async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine(Description);
        Console.Write("Enter your JQL query: ");
        var jql = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(jql))
        {
            return;
        }

        var issues = await this.runner.SearchJiraIssuesWithJqlAsync(jql, Fields);
        Console.WriteLine($"{issues.Count} issues fetched.");

        this.exporter.Export(issues);
    }
}
