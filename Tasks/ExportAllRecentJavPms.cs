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
        var runner = new JiraQueryRunner();
        var issues = await runner.SearchJiraIssuesWithJqlAsync(jql, fields);
        Console.WriteLine($"{issues.Count} issues fetched.");
        var exporter = new CsvExporter();
        var fileName = exporter.Export(issues);
        Console.WriteLine(Path.GetFullPath(fileName));
    }
}
