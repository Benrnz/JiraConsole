using Microsoft.VisualBasic.CompilerServices;

namespace BensJiraConsole.Tasks;

// ReSharper disable once UnusedType.Global
public class ExportSprintTicketsNoEstimateTask : IJiraExportTask
{
    public string Key => "NOESTIMATE";

    public string Description => "Export Active Sprint tickets with no estimate (Superclass, Ruby Ducks, Spearhead only)";

    public FieldMapping[] Fields =>
    [
        //  JIRA Field Name, Friendly Alias, Flatten object with field name
        new("summary"),
        new("status", "Status", "name"),
        new("issuetype", "IssueType", "name"),
        new("customfield_11934", "DevTimeSpent"),
        new("parent", "Parent", "key"),
        new("customfield_10004", "StoryPoints"),
        new("created"),
        new("assignee", "Assignee", "displayName"),
        new("customfield_11903", "BugType", "value"),
        new("customfield_11812", "CustomersMultiSelect", "value"),
        new("customfield_11906", "Category", "value"),
        new("resolution", "Resolution", "name"),
        new("timeoriginalestimate", "Original Estimate"),
        new("customfield_12236", "Flag Count"),
        new("customfield_11899", "Severity", "value"),
        new("customfield_10007", "Sprint", "name"),
        new("priority", "Priority", "name"),
        new("resolutiondate", "Resolved"),
        new("customfield_11400", "Team", "name"),
    ];


    public async Task ExecuteAsync(string[] fields)
    {
        Console.WriteLine(Description);
        var runner = new JiraQueryDynamicRunner();
        var jql = "project=JAVPM AND sprint IN openSprints() AND \"Story Points[Number]\" IN (EMPTY, 0) AND \"Team[Team]\" IN (f08f7fdc-cfab-4de7-8fdd-8da57b10adb6, 60412efa-7e2e-4285-bb4e-f329c3b6d417, 1a05d236-1562-4e58-ae88-1ffc6c5edb32)";
        Console.WriteLine(jql);
        var issues = await runner.SearchJiraIssuesWithJqlAsync(jql, Fields);
        Console.WriteLine($"{issues.Count} issues fetched.");

        if (issues.Count < 20)
        {
            issues.ForEach(i => Console.WriteLine($"{i.key}"));
        }

        var exporter = new SimpleCsvExporter(Key);
        exporter.Export(issues);
    }
}
