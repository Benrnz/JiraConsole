namespace BensJiraConsole.Tasks;

// ReSharper disable once UnusedType.Global
public class ExportJqlQueryTask : IJiraExportTask
{
    public string Key => "JQL";

    public string Description => "Export issues matching a JQL query";

    public async Task ExecuteAsync(string[] fields)
    {
        Console.Write("Enter your JQL query: ");
        var jql = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(jql))
        {
            return;
        }

        var issues = await PostSearchJiraIssueAsync(jql, fields);
        var exporter = new CsvExporter();
        var fileName = exporter.Export(issues);
        Console.WriteLine(Path.GetFullPath(fileName));
    }

    private async Task<List<JiraIssue>> PostSearchJiraIssueAsync(string jql, string[] fields)
    {
        var client = new JiraApiClient();
        var responseJson = await client.PostSearchJqlAsync(jql, fields);
        var mapper = new JiraIssueMapper();
        return mapper.MapToJiraIssue(responseJson);
    }
}
