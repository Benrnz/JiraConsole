using BensEngineeringMetrics.Jira;

namespace BensEngineeringMetrics.Tasks;

// ReSharper disable once UnusedType.Global
public class ExportSprintTicketsNoEstimateTask(IJiraQueryRunner runner, ICsvExporter exporter) : IJiraExportTask
{
    private const string KeyString = "NOESTIMATE";

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

    public string Description => "Export Active Sprint tickets with _No_Estimate_ (Superclass, Ruby Ducks, Spearhead only)";


    public async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine(Description);
        var jql =
            "project=JAVPM AND type != Epic AND sprint IN openSprints() AND \"Story Points[Number]\" IN (EMPTY, 0) AND \"Team[Team]\" IN (f08f7fdc-cfab-4de7-8fdd-8da57b10adb6, 60412efa-7e2e-4285-bb4e-f329c3b6d417, 1a05d236-1562-4e58-ae88-1ffc6c5edb32)";
        Console.WriteLine(jql);
        var issues = await runner.SearchJiraIssuesWithJqlAsync(jql, Fields);
        Console.WriteLine($"{issues.Count} issues fetched.");

        if (issues.Count < 20)
        {
            foreach (var i in issues.ToList())
            {
                Console.WriteLine($"{i.key} {i.Team}");
            }
        }

        exporter.SetFileNameMode(FileNameMode.Hint, Key);
        exporter.Export(issues);
    }
}
