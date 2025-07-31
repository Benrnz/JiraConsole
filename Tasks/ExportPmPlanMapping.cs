namespace BensJiraConsole.Tasks;

// ReSharper disable once UnusedType.Global
public class ExportPmPlanMapping : IJiraExportTask
{
    public string Key => "PMPLAN_STORIES";
    public string Description => "Export PM Plan mapping";

    public async Task ExecuteAsync(string[] fields)
    {
        Console.WriteLine(Description);
        var jqlPmPlans = "IssueType = Idea AND \"PM Customer[Checkboxes]\"= Envest ORDER BY Key";
        Console.WriteLine(jqlPmPlans);
        var childrenJql = "project=JAVPM AND (issue in (linkedIssues(\"{0}\")) OR parent in (linkedIssues(\"{0}\"))) ORDER BY key";
        Console.WriteLine($"ForEach PMPLAN: {childrenJql}");
        var pmPlans = await PostSearchJiraIdeaAsync(jqlPmPlans, ["key", "summary", "status", "customfield_11986", "customfield_12038", "customfield_12137"]);

        var allIssues = new Dictionary<string, JiraIssue>(); // Ensure the final list of JAVPMs is unique NO DUPLICATES
        foreach (var pmPlan in pmPlans)
        {
            var children = await PostSearchJiraIssueAsync(string.Format(childrenJql, pmPlan.Key), fields);
            Console.WriteLine($"Fetched {children.Count} children for {pmPlan}");
            children.ForEach(c =>
            {
                c.PmPlan = pmPlan;
                allIssues.TryAdd(c.Key, c);
            });
        }

        Console.WriteLine($"Found {allIssues.Count} unique stories");
        var exporter = new CsvExporter();
        var fileName = exporter.Export(allIssues.Values);
        Console.WriteLine(Path.GetFullPath(fileName));
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
