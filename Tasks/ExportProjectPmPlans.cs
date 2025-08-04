namespace BensJiraConsole.Tasks;

// ReSharper disable once UnusedType.Global
public class ExportProjectPmPlans : IJiraExportTask
{
    public string Key => "PMPLANS";
    public string Description => "Export PM Plans for Envest";

    public FieldMapping[] Fields =>
    [
        //  JIRA Field Name, Friendly Alias, Flatten object with field name
        new("summary", "Summary"),
        new("status", "Status", "name"),
        new("issuetype", "IssueType", "name"),
        new("customfield_12038", "PmPlan High Level Estimate"),
        new("customfield_12137", "Estimation Status", "value"), 
        new("customfield_11986", "Is Reqd For GoLive")
    ];

    public async Task ExecuteAsync(string[] fields)
    {
        Console.WriteLine(Description);
        var jqlPmPlans = "IssueType = Idea AND \"PM Customer[Checkboxes]\"= Envest ORDER BY Key";
        Console.WriteLine(jqlPmPlans);
        var runner = new JiraQueryDynamicRunner();
        var pmPlans = await runner.SearchJiraIssuesWithJqlAsync(jqlPmPlans, Fields);
        var exporter = new SimpleCsvExporter();
        var fileName = exporter.Export(pmPlans);
        Console.WriteLine(Path.GetFullPath(fileName));
    }
}
