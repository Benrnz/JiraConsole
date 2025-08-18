namespace BensJiraConsole.Tasks;

public record FieldMapping(string Field, string Alias = "", string FlattenField = "");

public class ExportAllRecentJavPms : IJiraExportTask
{
    /// <summary> Fields to include in the export: </summary>
    public FieldMapping[] Fields =>
    [
        //  JIRA Field Name,          Friendly Alias,                    Flatten object field name
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
        new("customfield_11400", "Team", "name")
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
