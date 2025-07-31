namespace BensJiraConsole.Tasks;

public class ExportAllRecentJavPms : IJiraExportTask
{
    public string Key => "JAVPMs";
    public string Description => "Export all JAVPM tickets from the last 18 months.";

    public async Task ExecuteAsync(string[] fields)
    {
        Console.WriteLine(Description);
        var jql = "project=JAVPM AND created > -540d ORDER BY created";
        Console.WriteLine(jql);
        var issues = await PostSearchJiraIssueAsync(jql, fields);
        Console.WriteLine($"{issues.Count} issues fetched.");
        var exporter = new CsvExporter();
        var fileName = exporter.Export(issues);
        Console.WriteLine(Path.GetFullPath(fileName));
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
