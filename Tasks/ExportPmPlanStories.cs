namespace BensJiraConsole.Tasks;

// ReSharper disable once UnusedType.Global
public class ExportPmPlanStories : IJiraExportTask
{
    private static readonly FieldMapping[] Fields =
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
        JiraFields.Created,
    ];

    private static readonly FieldMapping[] PmPlanFields =
    [
        JiraFields.Summary,
        JiraFields.Status,
        JiraFields.IssueType,
        JiraFields.PmPlanHighLevelEstimate,
        JiraFields.EstimationStatus,
        JiraFields.StoryPoints,
        JiraFields.IsReqdForGoLive
    ];

    public IEnumerable<dynamic> PmPlans { get; private set; } = [];

    public string Key => "PMPLAN_STORIES";
    public string Description => "Export _PMPlan_children_stories_";

    public async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine(Description);
        var allIssues = await RetrieveAllStoriesMappingToPmPlan();
        Console.WriteLine($"Found {allIssues.Count} unique stories");
        var exporter = new SimpleCsvExporter(Key);
        exporter.Export(allIssues);
    }

    public async Task<IReadOnlyList<JiraIssueWithPmPlan>> RetrieveAllStoriesMappingToPmPlan(string? additionalCriteria = null)
    {
        additionalCriteria = additionalCriteria ?? string.Empty;
        var jqlPmPlans = "IssueType = Idea AND \"PM Customer[Checkboxes]\"= Envest ORDER BY Key";
        Console.WriteLine(jqlPmPlans);
        var childrenJql = $"project=JAVPM AND (issue in (linkedIssues(\"{{0}}\")) OR parent in (linkedIssues(\"{{0}}\"))) {additionalCriteria} ORDER BY key";
        Console.WriteLine($"ForEach PMPLAN: {childrenJql}");
        var dynamicRunner = new JiraQueryDynamicRunner();
        PmPlans = await dynamicRunner.SearchJiraIssuesWithJqlAsync(jqlPmPlans, PmPlanFields);

        var allIssues = new Dictionary<string, JiraIssueWithPmPlan>(); // Ensure the final list of JAVPMs is unique NO DUPLICATES
        foreach (var pmPlan in PmPlans)
        {
            var children = await dynamicRunner.SearchJiraIssuesWithJqlAsync(string.Format(childrenJql, pmPlan.key), Fields);
            Console.WriteLine($"Fetched {children.Count} children for {pmPlan.key}");
            foreach (var child in children)
            {
                JiraIssueWithPmPlan issue = CreateJiraIssueWithPmPlan(child, pmPlan);
                allIssues.TryAdd(issue.Key, issue);
            }
        }

        return allIssues.Values.ToList();
    }

    private JiraIssueWithPmPlan CreateJiraIssueWithPmPlan(dynamic i, dynamic pmPlan)
    {
        var storyPointsField = JiraFields.StoryPoints.Parse<double?>(i) ?? 0.0;

        var typedIssue = new JiraIssueWithPmPlan(
            pmPlan.key,
            JiraFields.Key.Parse<string>(i),
            JiraFields.Summary.Parse<string>(i),
            JiraFields.Status.Parse<string>(i),
            JiraFields.IssueType.Parse<string>(i),
            storyPointsField,
            JiraFields.IsReqdForGoLive.Parse<bool>(pmPlan),
            JiraFields.EstimationStatus.Parse<string?>(pmPlan),
            JiraFields.PmPlanHighLevelEstimate.Parse<double>(pmPlan),
            JiraFields.Created.Parse<DateTimeOffset>(i),
            JiraFields.ParentKey.Parse<string?>(i));
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
        string? ParentEpic = null);
}
