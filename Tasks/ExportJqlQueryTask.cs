namespace BensJiraConsole.Tasks;

// ReSharper disable once UnusedType.Global
public class ExportJqlQueryTask : IJiraExportTask
{
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
        new("customfield_11400", "Team", "name")
    ];

    public string Key => "JQL";

    public string Description => "Export issues matching a JQL query";


    public async Task ExecuteAsync(string[] fields)
    {
        Console.WriteLine(Description);
        Console.Write("Enter your JQL query: ");
        var jql = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(jql))
        {
            return;
        }

        var runner = new JiraQueryDynamicRunner();
        var issues = await runner.SearchJiraIssuesWithJqlAsync(jql, Fields);
        Console.WriteLine($"{issues.Count} issues fetched.");

        var exporter = new SimpleCsvExporter(Key);
        exporter.Export(issues);
    }
}
