namespace BensJiraConsole.Tasks;

public class InitiativeProgressTableTask : IJiraExportTask
{
    private const string GoogleSheetId = "1OVUx08nBaD8uH-klNAzAtxFSKTOvAAk5Vnm11ALN0Zo";
    private const string TaskKey = "INIT_TABLE";
    private const string ProductInitiativePrefix = "PMPLAN-";

    private static readonly IFieldMapping[] IssueFields =
    [
        JiraFields.Summary,
        JiraFields.Status,
        JiraFields.ParentKey,
        JiraFields.StoryPoints,
        JiraFields.OriginalEstimate,
        JiraFields.Created,
        JiraFields.Resolved
    ];

    private static readonly IFieldMapping[] PmPlanFields =
    [
        JiraFields.Summary,
        JiraFields.Status,
        JiraFields.IssueType,
        JiraFields.PmPlanHighLevelEstimate,
        JiraFields.EstimationStatus,
        JiraFields.ProjectTarget
    ];

    private readonly ICsvExporter exporter = new SimpleCsvExporter(TaskKey) { Mode = FileNameMode.ExactName };

    private readonly IJiraQueryRunner runner = new JiraQueryDynamicRunner();

    private readonly IWorkSheetReader sheetReader = new GoogleSheetReader(GoogleSheetId);

    private readonly IWorkSheetUpdater sheetUpdater = new GoogleSheetUpdater(GoogleSheetId);

    public IList<JiraInitiative> AllInitiativesData { get; private set; }

    public IDictionary<string, IReadOnlyList<JiraIssue>> AllIssuesData { get; private set; }

    public string Description => "Export and update Initiative level PMPLAN data for drawing feature-set release _burn-up_charts_";

    public string Key => TaskKey;

    public async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine(Description);

        await LoadData();

        var reportArray = BuildReportArray(AllInitiativesData);
        await this.sheetUpdater.ClearSheet("Summary", "A2:Z10000");
        await this.sheetUpdater.EditSheet("'Summary'!A2", reportArray, true);
    }

    public async Task LoadData()
    {
        var initiativeKeys = await GetInitiativesForReport();
        if (!initiativeKeys.Any())
        {
            Console.WriteLine("No Product Initiatives found to process. Exiting.");
            return;
        }

        await ExtractAllInitiativeData(initiativeKeys);
    }

    private static IList<IList<object?>> BuildReportArray(IList<JiraInitiative> allInitiativeData)
    {
        IList<IList<object?>> reportArray = new List<IList<object?>>();
        foreach (var initiative in allInitiativeData)
        {
            var row = new List<object?>();
            row.Add(initiative.Description);
            row.Add(initiative.InitiativeKey);
            row.Add(initiative.Progress.Total);
            row.Add(initiative.Progress.Done);
            row.Add(initiative.Progress.Remaining);
            row.Add(initiative.Progress.PercentDone);
            row.Add(initiative.Status);
            row.Add(initiative.Target?.ToString("d MMM yy"));
            reportArray.Add(row);
            foreach (var childPmPlan in initiative.PmPlans)
            {
                var childRow = new List<object?>();
                childRow.Add(childPmPlan.Description);
                childRow.Add(childPmPlan.PmPlanKey);
                childRow.Add(childPmPlan.Progress.Total);
                childRow.Add(childPmPlan.Progress.Done);
                childRow.Add(childPmPlan.Progress.Remaining);
                childRow.Add(childPmPlan.Progress.PercentDone);
                childRow.Add(childPmPlan.Status);
                childRow.Add(childPmPlan.Target?.ToString("d MMM yy"));
                reportArray.Add(childRow);
            }

            // Spacer empty row
            reportArray.Add(new List<object?>());
        }

        return reportArray;
    }

    private JiraIssue CreateJiraIssue(string initiative, dynamic issue)
    {
        var storyPoints = JiraFields.StoryPoints.Parse(issue);
        if (storyPoints is null)
        {
            // If no story points, try to estimate from original estimate (in seconds)
            var originalEstimate = JiraFields.OriginalEstimate.Parse(issue);
            if (originalEstimate is not null)
            {
                storyPoints = Math.Round((double)originalEstimate / 3600 / 8, 1);
            }
        }

        return new JiraIssue(
            JiraFields.Key.Parse(issue)!,
            JiraFields.Created.Parse(issue),
            JiraFields.Resolved.Parse(issue),
            JiraFields.Status.Parse(issue) ?? Constants.Unknown,
            storyPoints ?? 0.0,
            initiative);
    }

    private async Task ExtractAllInitiativeData(IReadOnlyList<string> initiativeKeys)
    {
        var pmPlanJql = "(issue in linkedIssues(\"{0}\") OR parent in linkedIssues(\"{0}\")) AND \"Required for Go-live[Checkbox]\" = 1 ORDER BY key";
        var javPmKeyql = "project = JAVPM AND (issue in (linkedIssues(\"{0}\")) OR parent in (linkedIssues(\"{0}\"))) ORDER BY key";

        var allInitiativeData = new List<JiraInitiative>();
        AllIssuesData = new Dictionary<string, IReadOnlyList<JiraIssue>>();
        foreach (var initiative in initiativeKeys)
        {
            Console.WriteLine($"* Finding all work for {initiative}");
            var jiraInitiative = await GetInitiativeDetails(initiative);
            var pmPlans = await this.runner.SearchJiraIssuesWithJqlAsync(string.Format(pmPlanJql, initiative), PmPlanFields);

            var allIssues = new List<JiraIssue>();
            foreach (var pmPlan in pmPlans)
            {
                string pmPlanKey = JiraFields.Key.Parse(pmPlan);
                string summary = JiraFields.Summary.Parse(pmPlan) ?? string.Empty;
                string status = JiraFields.Status.Parse(pmPlan) ?? Constants.Unknown;
                DateTimeOffset? target = JiraFields.ProjectTarget.Parse(pmPlan);
                var pmPlanData = new JiraPmPlan(pmPlanKey, summary, new StatLine(), status, target);
                var children = await this.runner.SearchJiraIssuesWithJqlAsync(string.Format(javPmKeyql, pmPlanKey), IssueFields);
                Console.WriteLine($"Fetched {children.Count} children for {pmPlan.key}");
                var range = children.Select<dynamic, JiraIssue>(i => CreateJiraIssue(initiative, i)).ToList();
                pmPlanData.Progress.Total = range.Sum(i => i.StoryPoints);
                pmPlanData.Progress.Done = range.Where(i => i.Status == Constants.DoneStatus).Sum(i => i.StoryPoints);
                allIssues.AddRange(range);
                jiraInitiative.PmPlans.Add(pmPlanData);
            }

            Console.WriteLine($"Found {allIssues.Count} unique stories");
            jiraInitiative.Progress.Total = jiraInitiative.PmPlans.Sum(p => p.Progress.Total);
            jiraInitiative.Progress.Done = jiraInitiative.PmPlans.Sum(p => p.Progress.Done);
            AllIssuesData.Add(initiative, allIssues);
            allInitiativeData.Add(jiraInitiative);
        } // For each initiative

        AllInitiativesData = allInitiativeData;
    }

    private async Task<JiraInitiative> GetInitiativeDetails(string initiativeKey)
    {
        var result = await this.runner.SearchJiraIssuesWithJqlAsync($"key={initiativeKey}", [JiraFields.Summary, JiraFields.Status, JiraFields.ProjectTarget]);
        var single = result.Single();
        string summary = JiraFields.Summary.Parse(single) ?? string.Empty;
        string status = JiraFields.Status.Parse(single) ?? Constants.Unknown;
        DateTimeOffset? target = JiraFields.ProjectTarget.Parse(single);
        return new JiraInitiative(initiativeKey, summary, new List<JiraPmPlan>(), new StatLine(), status, target);
    }

    private async Task<IReadOnlyList<string>> GetInitiativesForReport()
    {
        var list = await this.sheetReader.GetSheetNames();
        var initiatives = list.Where(x => x.StartsWith(ProductInitiativePrefix)).ToList();
        Console.WriteLine("Updating burn-up charts for the following Product Iniiatives:");
        foreach (var initiative in initiatives)
        {
            Console.WriteLine($"*   {initiative}");
        }

        return initiatives;
    }

    public record JiraInitiative(string InitiativeKey, string Description, IList<JiraPmPlan> PmPlans, StatLine Progress, string Status, DateTimeOffset? Target = null);

    public record JiraPmPlan(string PmPlanKey, string Description, StatLine Progress, string Status, DateTimeOffset? Target = null);

    public record StatLine
    {
        public double Done { get; set; }

        public double PercentDone => Done / (Total == 0 ? 1 : Total);
        public double Remaining => Total - Done;
        public double Total { get; set; }
    }

    public record JiraIssue(string Key, DateTimeOffset CreatedDateTime, DateTimeOffset? ResolvedDateTime, string Status, double StoryPoints, string PmPlan);
}
