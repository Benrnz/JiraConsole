namespace BensJiraConsole;

// ReSharper disable once UnusedType.Global
public class ExportJqlQueryTask : IJiraExportTask
{
    public string Key => "JQL";

    public string Description => "Export issues matching a JQL query";

    public async Task<List<JiraIssue>> ExecuteAsync(string[] fields)
    {
        Console.Write("Enter your JQL query: ");
        var jql = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(jql))
        {
            return new List<JiraIssue>();
        }

        var issues = await PostSearchJiraIssueAsync(jql, fields);
        return issues;
    }

    private async Task<List<JiraIssue>> PostSearchJiraIssueAsync(string jql, string[] fields)
    {
        var client = new JiraApiClient();
        var responseJson = await client.PostSearchJqlAsync(jql, fields);
        var mapper = new JiraIssueMapper();
        return mapper.MapToJiraIssue(responseJson);
    }
}
