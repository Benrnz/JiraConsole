namespace BensJiraConsole.Tasks;

// ReSharper disable once UnusedType.Global
public class ExportProjectPmPlans : IJiraExportTask
{
    private static readonly IFieldMapping[] Fields =
    [
        JiraFields.Summary,
        JiraFields.Status,
        JiraFields.IssueType,
        JiraFields.PmPlanHighLevelEstimate,
        JiraFields.EstimationStatus,
        JiraFields.IsReqdForGoLive
    ];

    public string Key => "PMPLANS";
    public string Description => "Export _PMPlans_ for Envest";

    public async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine(Description);
        var jqlPmPlans = "IssueType = Idea AND \"PM Customer[Checkboxes]\"= Envest ORDER BY Key";
        Console.WriteLine(jqlPmPlans);
        var runner = new JiraQueryDynamicRunner();
        var pmPlans = await runner.SearchJiraIssuesWithJqlAsync(jqlPmPlans, Fields);
        var exporter = new SimpleCsvExporter(Key) { Mode = FileNameMode.ExactName };
        exporter.Export(pmPlans, Key);
    }
}
