namespace BensJiraConsole.Tasks;

// ReSharper disable once UnusedType.Global
public class ExportPmPlanMapping : IJiraExportTask
{
    public string Key => "PMPLAN_STORIES";
    public string Description => "Export PM Plan mapping";

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

    public async Task ExecuteAsync(string[] fields)
    {
        Console.WriteLine(Description);
        var jqlPmPlans = "IssueType = Idea AND \"PM Customer[Checkboxes]\"= Envest ORDER BY Key";
        Console.WriteLine(jqlPmPlans);
        var childrenJql = "project=JAVPM AND (issue in (linkedIssues(\"{0}\")) OR parent in (linkedIssues(\"{0}\"))) ORDER BY key";
        Console.WriteLine($"ForEach PMPLAN: {childrenJql}");
        var runner = new JiraQueryRunner();
        var pmPlans = await runner.SearchJiraIdeaWithJqlAsync(jqlPmPlans, ["key", "summary", "status", "customfield_11986", "customfield_12038", "customfield_12137"]);

        var allIssues = new Dictionary<string, dynamic>(); // Ensure the final list of JAVPMs is unique NO DUPLICATES
        var dynamicRunner = new JiraQueryDynamicRunner();
        foreach (var pmPlan in pmPlans)
        {
            var children = await dynamicRunner.SearchJiraIssuesWithJqlAsync(string.Format(childrenJql, pmPlan.Key), Fields);
            Console.WriteLine($"Fetched {children.Count} children for {pmPlan}");
            children.ForEach(c =>
            {
                c.PmPlan = pmPlan.Key;
                c.IsReqdForGoLive = pmPlan.RequiredForGoLive;
                c.PmPlanEstimationStatus = pmPlan.EstimationStatus;
                allIssues.TryAdd(c.key, c);
            });
        }

        Console.WriteLine($"Found {allIssues.Count} unique stories");
        var exporter = new SimpleCsvExporter();
        var fileName = exporter.Export(allIssues.Values);
        Console.WriteLine(Path.GetFullPath(fileName));
    }
}
