namespace BensJiraConsole.Tasks;

// ReSharper disable once UnusedType.Global
public class ExportProjectPmPlans : IJiraExportTask
{
    public string Key => "PMPLANS";
    public string Description => "Export PM Plans for Envest";

    public async Task ExecuteAsync(string[] fields)
    {
        Console.WriteLine(Description);
        var jqlPmPlans = "IssueType = Idea AND \"PM Customer[Checkboxes]\"= Envest ORDER BY Key";
        var pmPlans = await PostSearchJiraIdeaAsync(jqlPmPlans, ["key", "summary", "status", "customfield_12038", "customfield_11986", "customfield_12137"]);
        //                                                                                                        PmPlanHighLevelEstimate, RequiredForGoLive, EstimationStatus

        var exporter = new CsvExporter();
        var fileName = exporter.Export(pmPlans);
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
}
