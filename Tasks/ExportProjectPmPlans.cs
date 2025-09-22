namespace BensJiraConsole.Tasks;

// ReSharper disable once UnusedType.Global
public class ExportProjectPmPlans : IJiraExportTask
{
    private const string KeyString = "PMPLANS";

    private static readonly IFieldMapping[] Fields =
    [
        JiraFields.Summary,
        JiraFields.Status,
        JiraFields.IssueType,
        JiraFields.PmPlanHighLevelEstimate,
        JiraFields.EstimationStatus,
        JiraFields.IsReqdForGoLive
    ];

    private readonly ICsvExporter exporter = new SimpleCsvExporter(KeyString);
    private readonly IJiraQueryRunner runner = new JiraQueryDynamicRunner();

    public string Key => KeyString;
    public string Description => "Export _PMPlans_ for Envest";

    public async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine(Description);
        var jqlPmPlans = "IssueType = Idea AND \"PM Customer[Checkboxes]\"= Envest ORDER BY Key";
        Console.WriteLine(jqlPmPlans);
        var pmPlans = await this.runner.SearchJiraIssuesWithJqlAsync(jqlPmPlans, Fields);
        this.exporter.Mode = FileNameMode.ExactName;
        this.exporter.Export(pmPlans, Key);
    }
}
