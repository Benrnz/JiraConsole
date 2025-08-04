namespace BensJiraConsole.Tasks;

// ReSharper disable once UnusedType.Global
public class ExportPmPlanMapping : IJiraExportTask
{
    public string Key => "PMPLAN_STORIES";
    public string Description => "Export PM Plan children mapping";

    public FieldMapping[] Fields =>
    [
        //  JIRA Field Name,          Friendly Alias,                    Flatten object field name
        new("summary", "Summary"),
        new("status", "Status", "name"),
        new("parent", "Parent", "key"),
        new("customfield_10004", "StoryPoints"),
        new("timeoriginalestimate", "Original Estimate"),
        new("created"),
    ];
    public FieldMapping[] PmPlanFields =>
    [
        //  JIRA Field Name,          Friendly Alias,                    Flatten object field name
        new("summary", "Summary"),
        new("status", "Status", "name"),
        new("issuetype", "IssueType", "name"),
        new("customfield_12038", "PmPlanHighLevelEstimate"),
        new("customfield_12137", "EstimationStatus", "value"), 
        new("customfield_11986", "IsReqdForGoLive")
    ];

    public async Task ExecuteAsync(string[] fields)
    {
        Console.WriteLine(Description);
        var jqlPmPlans = "IssueType = Idea AND \"PM Customer[Checkboxes]\"= Envest ORDER BY Key";
        Console.WriteLine(jqlPmPlans);
        var childrenJql = "project=JAVPM AND (issue in (linkedIssues(\"{0}\")) OR parent in (linkedIssues(\"{0}\"))) ORDER BY key";
        Console.WriteLine($"ForEach PMPLAN: {childrenJql}");
        var dynamicRunner = new JiraQueryDynamicRunner();
        var pmPlans = await dynamicRunner.SearchJiraIssuesWithJqlAsync(jqlPmPlans, PmPlanFields);

        var allIssues = new Dictionary<string, dynamic>(); // Ensure the final list of JAVPMs is unique NO DUPLICATES
        foreach (var pmPlan in pmPlans)
        {
            var children = await dynamicRunner.SearchJiraIssuesWithJqlAsync(string.Format(childrenJql, pmPlan.key), Fields);
            Console.WriteLine($"Fetched {children.Count} children for {pmPlan.key}");
            foreach (var child in children)
            {
                child.PmPlan = pmPlan.key;
                child.IsReqdForGoLive = pmPlan.IsReqdForGoLive;
                child.PmPlanEstimationStatus = pmPlan.EstimationStatus;
                allIssues.TryAdd(child.key, child);
            }
        }

        Console.WriteLine($"Found {allIssues.Count} unique stories");
        var exporter = new SimpleCsvExporter();
        var fileName = exporter.Export(allIssues.Values);
        Console.WriteLine(Path.GetFullPath(fileName));
    }
}
