namespace BensJiraConsole.Tasks;

// ReSharper disable once UnusedType.Global
public class ExportActiveSprintTicketsNotPmPlans : IJiraExportTask
{
    public string Key => "SPRINT";
    public string Description => "Export Any Sprint ticket that does not map up to a PMPLAN (Superclass and Ruby Ducks only)";

    public async Task ExecuteAsync(string[] fields)
    {
        Console.WriteLine(Description);
        var jqlPmPlans = "IssueType = Idea AND \"PM Customer[Checkboxes]\"= Envest ORDER BY Key";
        Console.WriteLine(jqlPmPlans);
        var childrenJql = "project=JAVPM AND (issue in (linkedIssues(\"{0}\")) OR parent in (linkedIssues(\"{0}\"))) ORDER BY key";
        Console.WriteLine($"ForEach PMPLAN: {childrenJql}");

        var runner = new JiraQueryRunner();
        var pmPlans = await runner.SearchJiraIdeaWithJqlAsync(jqlPmPlans, ["key", "summary", "customfield_11986", "customfield_12038", "customfield_12137"]);

        var allIssues = new List<JiraIssue>();
        foreach (var pmPlan in pmPlans)
        {
            var children = await runner.SearchJiraIssuesWithJqlAsync(string.Format(childrenJql, pmPlan.Key), fields);
            Console.WriteLine($"Fetched {children.Count} stories for {pmPlan}");
            allIssues.AddRange(children);
        }

        jqlPmPlans = "project = \"JAVPM\" AND sprint IN openSprints() AND \"Team[Team]\" IN (1a05d236-1562-4e58-ae88-1ffc6c5edb32, 60412efa-7e2e-4285-bb4e-f329c3b6d417) ORDER BY key";
        var sprintWork = await runner.SearchJiraIssuesWithJqlAsync(jqlPmPlans, fields);
        var nonEnvestWork = new List<JiraIssue>();
        foreach (var sprintTicket in sprintWork)
        {
            if (allIssues.All(c => c.Key != sprintTicket.Key))
            {
                nonEnvestWork.Add(sprintTicket);
            }
        }

        var exporter = new CsvExporter();
        var fileName = exporter.Export(nonEnvestWork);
        Console.WriteLine(Path.GetFullPath(fileName));
    }
}
