using BensEngineeringMetrics.Jira;

namespace BensEngineeringMetrics.Tasks;

// ReSharper disable once UnusedType.Global
public class ExportPmPlanStories(IJiraQueryRunner runner, ICsvExporter exporter) : IJiraExportTask
{
    private const string KeyString = "PMPLAN_STORIES";

    private static readonly IFieldMapping[] Fields =
    [
        JiraFields.Summary,
        JiraFields.Status,
        JiraFields.ParentKey,
        JiraFields.StoryPoints,
        JiraFields.OriginalEstimate,
        JiraFields.Created,
        JiraFields.IssueType,
        JiraFields.ReporterDisplay,
        JiraFields.ParentKey,
        JiraFields.Created
    ];

    private static readonly IFieldMapping[] PmPlanFields =
    [
        JiraFields.Summary,
        JiraFields.Status,
        JiraFields.IssueType,
        JiraFields.PmPlanHighLevelEstimate,
        JiraFields.EstimationStatus,
        JiraFields.StoryPoints,
        JiraFields.IsReqdForGoLive
    ];

    private IReadOnlyList<JiraIssueWithPmPlan> cachedIssues = [];

    public IEnumerable<dynamic> PmPlans { get; private set; } = [];

    public string Key => KeyString;
    public string Description => "Export _PMPlan_children_stories_";

    public async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine(Description);
        var allIssues = await RetrieveAllStoriesMappingToPmPlan();
        Console.WriteLine($"Found {allIssues.Count} unique stories");
        exporter.SetFileNameMode(FileNameMode.Hint, Key);
        exporter.Export(allIssues);
    }

    public async Task<IReadOnlyList<JiraIssueWithPmPlan>> RetrieveAllStoriesMappingToPmPlan(string? additionalCriteria = null)
    {
        if (this.cachedIssues.Any())
        {
            return this.cachedIssues;
        }

        additionalCriteria ??= string.Empty;
        var jqlPmPlans = "IssueType = Idea AND \"PM Customer[Checkboxes]\"= Envest ORDER BY Key";
        Console.WriteLine(jqlPmPlans);
        var childrenJql = $"project=JAVPM AND (issue in (linkedIssues(\"{{0}}\")) OR parent in (linkedIssues(\"{{0}}\"))) {additionalCriteria} ORDER BY key";
        Console.WriteLine($"ForEach PMPLAN: {childrenJql}");
        PmPlans = await runner.SearchJiraIssuesWithJqlAsync(jqlPmPlans, PmPlanFields);

        var allIssues = new Dictionary<string, JiraIssueWithPmPlan>(); // Ensure the final list of JAVPMs is unique NO DUPLICATES
        foreach (var pmPlan in PmPlans)
        {
            var children = await runner.SearchJiraIssuesWithJqlAsync(string.Format(childrenJql, pmPlan.key), Fields);
            Console.WriteLine($"Fetched {children.Count} children for {pmPlan.key}");
            foreach (var child in children)
            {
                JiraIssueWithPmPlan issue = CreateJiraIssueWithPmPlan(child, pmPlan);
                allIssues.TryAdd(issue.Key, issue);
            }
        }

        return this.cachedIssues = allIssues.Values.ToList();
    }

    private JiraIssueWithPmPlan CreateJiraIssueWithPmPlan(dynamic i, dynamic pmPlan)
    {
        var storyPointsField = JiraFields.StoryPoints.Parse(i) ?? 0.0;

        var typedIssue = new JiraIssueWithPmPlan(
            pmPlan.key,
            JiraFields.Key.Parse(i),
            JiraFields.Summary.Parse(i),
            JiraFields.Status.Parse(i),
            JiraFields.IssueType.Parse(i),
            storyPointsField,
            JiraFields.IsReqdForGoLive.Parse(pmPlan),
            JiraFields.EstimationStatus.Parse(pmPlan),
            JiraFields.PmPlanHighLevelEstimate.Parse(pmPlan),
            JiraFields.Created.Parse(i),
            JiraFields.Summary.Parse(pmPlan),
            JiraFields.ParentKey.Parse(i));
        return typedIssue;
    }

    public record JiraIssueWithPmPlan(
        string PmPlan,
        string Key,
        string Summary,
        string Status,
        string Type,
        double StoryPoints,
        bool IsReqdForGoLive,
        string? EstimationStatus,
        double PmPlanHighLevelEstimate,
        DateTimeOffset CreatedDateTime,
        string PmPlanSummary,
        string? ParentEpic = null);
}
