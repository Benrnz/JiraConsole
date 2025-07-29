namespace BensJiraConsole;

// ReSharper disable once UnusedType.Global
public class ExportPmPlanMapping : IJiraExportTask
{
    public string Key => "PMPLAN";
    public string Description => "Export PM Plan mapping";

    public async Task<List<JiraIssue>> ExecuteAsync(string[] fields)
    {
        Console.WriteLine("Exporting a mapping of PMPlans to Stories.");
        var jqlPmPlans = "IssueType = Idea AND \"PM Customer[Checkboxes]\"= Envest ORDER BY Key";
        var pmPlans = await PostSearchJiraIdeaAsync(jqlPmPlans, ["key", "summary", "customfield_11986", "customfield_12038", "customfield_12137"]);

        var allIssues = new List<JiraIssue>();
        foreach (var pmPlan in pmPlans)
        {
            var jql = $"parent in (linkedIssues(\"{pmPlan.Key}\")) AND issuetype=Story ORDER BY key";
            var children = await PostSearchJiraIssueAsync(jql, fields);
            Console.WriteLine($"Exported {children.Count} stories for {pmPlan}");
            children.ForEach(c => c.PmPlan = pmPlan);
            allIssues.AddRange(children);
        }

        return allIssues;
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
