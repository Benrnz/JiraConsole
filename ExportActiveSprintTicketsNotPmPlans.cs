namespace BensJiraConsole;

// ReSharper disable once UnusedType.Global
public class ExportActiveSprintTicketsNotPmPlans : IJiraExportTask
{
    public string Key => "SPRINT";
    public string Description => "Export Any Sprint ticket that does not map up to a PMPLAN (Superclass and Ruby Ducks only)";

    public async Task<List<JiraIssue>> ExecuteAsync(string[] fields)
    {
        Console.WriteLine("Exporting a mapping of PMPlans to Stories.");
        var jql = "IssueType = Idea AND \"PM Customer[Checkboxes]\"= Envest ORDER BY Key";
        var pmPlans = await PostSearchJiraIdeaAsync(jql, ["key", "summary", "customfield_11986", "customfield_12038", "customfield_12137"]);

        var allIssues = new List<JiraIssue>();
        foreach (var pmPlan in pmPlans)
        {
            jql = $"parent in (linkedIssues(\"{pmPlan.Key}\")) AND issuetype=Story ORDER BY key";
            var children = await PostSearchJiraIssueAsync(jql, fields);
            Console.WriteLine($"Exported {children.Count} stories for {pmPlan}");
            children.ForEach(c => c.PmPlan = pmPlan);
            allIssues.AddRange(children);
        }

        jql = "project = \"JAVPM\" AND sprint IN openSprints() AND \"Team[Team]\" IN (1a05d236-1562-4e58-ae88-1ffc6c5edb32, 60412efa-7e2e-4285-bb4e-f329c3b6d417) ORDER BY key";
        var sprintWork = await PostSearchJiraIssueAsync(jql, fields);
        var nonEnvestWork = new List<JiraIssue>();
        foreach (var sprintTicket in sprintWork)
        {
            if (allIssues.All(c => c.Key != sprintTicket.Key))
            {
                nonEnvestWork.Add(sprintTicket);
            }
        }

        return nonEnvestWork;
    }

    private async Task<List<JiraPmPlan>> PostSearchJiraIdeaAsync(string jql, string[] fields)
    {
        var client = new JiraApiClient();
        var responseJson = await client.PostSearchJqlAsync(jql, fields);
        var mapper = new JiraIssueMapper();
        var results = mapper.MapToPmPlan(responseJson);
        while (!mapper.WasLastPage)
        {
            Console.WriteLine("    Fetching next page of results...");
            responseJson = await client.PostSearchJqlAsync(jql, fields, mapper.NextPageToken);
            var moreResults = mapper.MapToPmPlan(responseJson);
            results.AddRange(moreResults);
        }

        return results;
    }

    private async Task<List<JiraIssue>> PostSearchJiraIssueAsync(string jql, string[] fields)
    {
        var client = new JiraApiClient();
        var responseJson = await client.PostSearchJqlAsync(jql, fields);
        var mapper = new JiraIssueMapper();
        var results = mapper.MapToJiraIssue(responseJson);
        while (!mapper.WasLastPage)
        {
            Console.WriteLine("    Fetching next page of results...");
            responseJson = await client.PostSearchJqlAsync(jql, fields, mapper.NextPageToken);
            var moreResults = mapper.MapToJiraIssue(responseJson);
            results.AddRange(moreResults);
        }

        return results;
    }
}
