namespace BensJiraConsole.Tasks;

public record FieldMapping(string Field, string Alias = "", string FlattenField = "");

public class ExportAllRecentJavPms : IJiraExportTask
{
    public string Key => "JAVPMs";
    public string Description => "Export all JAVPM tickets from the last 18 months.";

    /// <summary> Fields to include in the export: </summary>
    public FieldMapping[] Fields =>
    [
        //  JIRA Field Name,          Friendly Alias,                    Flatten object field name
        new("summary"),
        new("status",            "Status",               "name"),
        new("issuetype",         "IssueType",            "name"),
        new("customfield_11934", "DevTimeSpent"),
        new("parent",            "Parent",               "key"),
        new("customfield_10004", "StoryPoints"),
        new("created"),
        new("assignee",          "Assignee",             "displayName"),
        new("customfield_11903", "BugType",              "value"),
        new("customfield_11812", "CustomersMultiSelect", "value"),
        new("customfield_11906", "Category",             "value")
    ];

    public async Task ExecuteAsync(string[] fields)
    {
        Console.WriteLine(Description);
        var jql = "project=JAVPM AND created > -540d ORDER BY created"; //540 days = 18 months
        Console.WriteLine(jql);
        var runner = new JiraQueryDynamicRunner();
        var issues = await runner.SearchJiraIssuesWithJqlAsync(jql, Fields);
        Console.WriteLine($"{issues.Count} issues fetched.");

        var exporter = new SimpleCsvExporter();
        var fileName = exporter.Export(issues);
        Console.WriteLine(Path.GetFullPath(fileName));
    }
}
