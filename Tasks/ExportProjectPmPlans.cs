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
        Console.WriteLine(jqlPmPlans);
        var runner = new JiraQueryRunner();
        var pmPlans = await runner.SearchJiraIssueLinkedWithPmPlanAsync(jqlPmPlans, ["key", "summary", "status", "customfield_12038", "customfield_11986", "customfield_12137"]);
        //                                                                                                                                  PmPlanHighLevelEstimate, RequiredForGoLive, EstimationStatus

        var exporter = new CsvExporter();
        var fileName = exporter.Export(pmPlans);
        Console.WriteLine(Path.GetFullPath(fileName));
    }
}
