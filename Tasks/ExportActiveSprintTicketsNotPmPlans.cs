namespace BensJiraConsole.Tasks;

// ReSharper disable once UnusedType.Global
public class ExportActiveSprintTicketsNotPmPlans : IJiraExportTask
{
    public FieldMapping[] Fields =>
    [
        //  JIRA Field Name,          Friendly Alias,                    Flatten object field name
        new("summary", "Summary"),
        new("status", "Status", "name"),
        new("parent", "Parent", "key"),
        new("customfield_10004", "StoryPoints"),
        new("timeoriginalestimate", "Original Estimate"),
        new("created")
    ];

    public FieldMapping[] PmPlanFields =>
    [
        //  JIRA Field Name,          Friendly Alias,                    Flatten object field name
        new("summary", "Summary"),
        new("status", "Status", "name"),
        new("issuetype", "IssueType", "name"),
        new("customfield_12038", "PmPlan High Level Estimate"),
        new("customfield_12137", "Estimation Status", "value"),
        new("customfield_11986", "Is Reqd For GoLive")
    ];

    public string Key => "SPRINT";
    public string Description => "Export Any Sprint ticket that does not map up to a PMPLAN (Superclass and Ruby Ducks only)";

    public async Task ExecuteAsync(string[] fields)
    {
        Console.WriteLine(Description);
        var jqlPmPlans = "IssueType = Idea AND \"PM Customer[Checkboxes]\"= Envest ORDER BY Key";
        Console.WriteLine(jqlPmPlans);
        var childrenJql = "project=JAVPM AND (issue in (linkedIssues(\"{0}\")) OR parent in (linkedIssues(\"{0}\"))) ORDER BY key";
        Console.WriteLine($"ForEach PMPLAN: {childrenJql}");

        var dynamicRunner = new JiraQueryDynamicRunner();
        var pmPlans = await dynamicRunner.SearchJiraIssuesWithJqlAsync(jqlPmPlans, PmPlanFields);

        var allIssues = new List<dynamic>();
        foreach (var pmPlan in pmPlans)
        {
            var children = await dynamicRunner.SearchJiraIssuesWithJqlAsync(string.Format(childrenJql, pmPlan.key), Fields);
            Console.WriteLine($"Fetched {children.Count} stories for {pmPlan.key}");
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
            }
        }

        Console.WriteLine($"project = JAVPM AND key IN ({string.Join(", ", nonEnvestWork.Select(x => (string)x.key))}) ORDER BY key");

        Console.WriteLine($"{nonEnvestWork.Count} tickets found in open sprints that are not Envest work.");

        var exporter = new SimpleCsvExporter(Key);
        exporter.Export(nonEnvestWork);
    }
}
