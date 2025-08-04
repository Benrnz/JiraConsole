namespace BensJiraConsole.Tasks;

// ReSharper disable once UnusedType.Global
public class ExportActiveSprintTicketsNotPmPlans : IJiraExportTask
{
    public string Key => "SPRINT";
    public string Description => "Export Any Sprint ticket that does not map up to a PMPLAN (Superclass and Ruby Ducks only)";

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
        var pmPlans = await runner.SearchJiraIdeaWithJqlAsync(jqlPmPlans, ["key", "summary", "customfield_11986", "customfield_12038", "customfield_12137"]);

        var allIssues = new List<dynamic>();
        var dynamicRunner = new JiraQueryDynamicRunner();
        foreach (var pmPlan in pmPlans)
        {
            var children = await dynamicRunner.SearchJiraIssuesWithJqlAsync(string.Format(childrenJql, pmPlan.Key), Fields);
            Console.WriteLine($"Fetched {children.Count} stories for {pmPlan}");
            allIssues.AddRange(children);
        }

        jqlPmPlans = "project = \"JAVPM\" AND sprint IN openSprints() AND \"Team[Team]\" IN (1a05d236-1562-4e58-ae88-1ffc6c5edb32, 60412efa-7e2e-4285-bb4e-f329c3b6d417) ORDER BY key";
        var sprintWork = await dynamicRunner.SearchJiraIssuesWithJqlAsync(jqlPmPlans, Fields);
        var nonEnvestWork = new List<dynamic>();
        foreach (var sprintTicket in sprintWork)
        {
            if (allIssues.All(c => c.key != sprintTicket.key))
            {
                nonEnvestWork.Add(sprintTicket);
                Console.WriteLine(sprintTicket.key);
            }
        }

        Console.WriteLine($"{nonEnvestWork.Count} tickets found in open sprints that are not Envest work.");

        var exporter = new SimpleCsvExporter();
        var fileName = exporter.Export(nonEnvestWork);
        Console.WriteLine(Path.GetFullPath(fileName));
    }
}
