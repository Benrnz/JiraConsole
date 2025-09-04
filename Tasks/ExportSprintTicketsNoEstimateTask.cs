namespace BensJiraConsole.Tasks;

// ReSharper disable once UnusedType.Global
public class ExportSprintTicketsNoEstimateTask : IJiraExportTask
{
    private static readonly FieldMapping[] Fields =
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

    public string Key => "NOESTIMATE";

    public string Description => "Export Active Sprint tickets with no estimate (Superclass, Ruby Ducks, Spearhead only)";


    public async Task ExecuteAsync(string[] fields)
    {
        Console.WriteLine(Description);
        var runner = new JiraQueryDynamicRunner();
        var jql =
            "project=JAVPM AND sprint IN openSprints() AND \"Story Points[Number]\" IN (EMPTY, 0) AND \"Team[Team]\" IN (f08f7fdc-cfab-4de7-8fdd-8da57b10adb6, 60412efa-7e2e-4285-bb4e-f329c3b6d417, 1a05d236-1562-4e58-ae88-1ffc6c5edb32)";
        Console.WriteLine(jql);
        var issues = await runner.SearchJiraIssuesWithJqlAsync(jql, Fields);
        Console.WriteLine($"{issues.Count} issues fetched.");

        if (issues.Count < 20)
        {
            issues.ForEach(i => Console.WriteLine($"{i.key} {i.Team}"));
        }

        var exporter = new SimpleCsvExporter(Key);
        exporter.Export(issues);
    }
}
