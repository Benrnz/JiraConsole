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
        JiraFields.ReporterDisplay
    ];

    private static readonly FieldMapping[] PmPlanFields =
    [
        JiraFields.Summary,
        JiraFields.Status,
        JiraFields.IssueType,
        JiraFields.PmPlanHighLevelEstimate,
        JiraFields.EstimationStatus,
        JiraFields.IsReqdForGoLive
    ];

    public IEnumerable<dynamic> PmPlans { get; private set; } = [];

    public string Key => "PMPLAN_STORIES";
    public string Description => "Export _PMPlan_children_stories_";

    public async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine(Description);
        var allIssues = await RetrieveAllStoriesMappingToPmPlan();
        Console.WriteLine($"Found {allIssues.Values.Count} unique stories");
        var exporter = new SimpleCsvExporter(Key);
        exporter.Export(allIssues.Values);
    }

    public async Task<IDictionary<string, dynamic>> RetrieveAllStoriesMappingToPmPlan(string? additionalCriteria = null)
    {
        additionalCriteria = additionalCriteria ?? string.Empty;
        var jqlPmPlans = "IssueType = Idea AND \"PM Customer[Checkboxes]\"= Envest ORDER BY Key";
        Console.WriteLine(jqlPmPlans);
        var childrenJql = $"project=JAVPM AND (issue in (linkedIssues(\"{{0}}\")) OR parent in (linkedIssues(\"{{0}}\"))) {additionalCriteria} ORDER BY key";
        Console.WriteLine($"ForEach PMPLAN: {childrenJql}");
        var dynamicRunner = new JiraQueryDynamicRunner();
        PmPlans = await dynamicRunner.SearchJiraIssuesWithJqlAsync(jqlPmPlans, PmPlanFields);

        var allIssues = new Dictionary<string, dynamic>(); // Ensure the final list of JAVPMs is unique NO DUPLICATES
        foreach (var pmPlan in PmPlans)
        {
            var children = await dynamicRunner.SearchJiraIssuesWithJqlAsync(string.Format(childrenJql, pmPlan.key), Fields);
            Console.WriteLine($"Fetched {children.Count} children for {pmPlan.key}");
            foreach (var child in children)
            {
                child.PmPlan = pmPlan.key;
                child.IsReqdForGoLive = pmPlan.IsReqdForGoLive;
                child.EstimationStatus = pmPlan.EstimationStatus;
                child.PmPlanHighLevelEstimate = pmPlan.PmPlanHighLevelEstimate;
                allIssues.TryAdd(child.key, child);
            }
        }

        return allIssues;
    }
}
