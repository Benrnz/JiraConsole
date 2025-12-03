using BensEngineeringMetrics.Jira;

namespace BensEngineeringMetrics.Tasks;

// ReSharper disable once UnusedType.Global
public class ExportProjectPmPlans(IJiraQueryRunner runner, ICsvExporter exporter) : IEngineeringMetricsTask
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

    public string Key => KeyString;
    public string Description => "Export _PMPlans_ for Envest";

    public async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine($"{Key} - {Description}");
        var jqlPmPlans = "IssueType = Idea AND \"PM Customer[Checkboxes]\"= Envest ORDER BY Key";
        Console.WriteLine(jqlPmPlans);
        var pmPlans = await runner.SearchJiraIssuesWithJqlAsync(jqlPmPlans, Fields);
        exporter.SetFileNameMode(FileNameMode.ExactName, Key);
        exporter.Export(pmPlans);
    }
}
