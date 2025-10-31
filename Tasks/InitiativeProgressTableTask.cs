namespace BensJiraConsole.Tasks;

public class InitiativeProgressTableTask(IJiraQueryRunner runner, IWorkSheetReader sheetReader, IWorkSheetUpdater sheetUpdater) : IJiraExportTask
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

    public IList<JiraInitiative> AllInitiativesData { get; private set; } = new List<JiraInitiative>();

    public IDictionary<string, IReadOnlyList<JiraIssue>> AllIssuesData { get; private set; } = new Dictionary<string, IReadOnlyList<JiraIssue>>();

    public Guid Id => Guid.NewGuid();

    public string Description => "Export and update Initiative level PMPLAN data for drawing feature-set release _burn-up_charts_";

    public string Key => TaskKey;

    public async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine(Description);

        await LoadData();
        await sheetUpdater.Open(GoogleSheetId);

        // Update the Summary Tab
        var summaryReportArray = BuildSummaryReportArray(AllInitiativesData);
        sheetUpdater.ClearRange("Summary", "A2:Z10000");
        sheetUpdater.EditSheet("'Summary'!A2", summaryReportArray, true);

        // Update the OverviewGraph tab
        var overviewReportArray = BuildOverviewReportArray(AllInitiativesData);
        sheetUpdater.ClearRange("OverviewGraphs", "A2:Z10000");
        sheetUpdater.EditSheet("'OverviewGraphs'!A2", overviewReportArray, true);
        sheetUpdater.EditSheet("Info!B1", [[DateTime.Now.ToString("g")]]);
        await sheetUpdater.SubmitBatch();
    }

    public async Task LoadData()
    {
        if (AllInitiativesData.Any())
        {
            // Data already loaded
            return;
        }

        await sheetReader.Open(GoogleSheetId);
        var initiativeKeys = await GetInitiativesForReport();
        if (!initiativeKeys.Any())
        {
            Console.WriteLine("No Product Initiatives found to process. Exiting.");
            return;
        }

        await ExtractAllInitiativeData(initiativeKeys);
    }

    private static IList<IList<object?>> BuildOverviewReportArray(IList<JiraInitiative> allInitiativeData)
    {
        // 4 columns: Name, Key, Done, Remaining
        IList<IList<object?>> reportArray = new List<IList<object?>>();
        foreach (var initiative in allInitiativeData)
        {
            var row = new List<object?>
            {
                initiative.Description,
                initiative.InitiativeKey,
                initiative.Progress.Done,
                initiative.Progress.Remaining
            };
            reportArray.Add(row);
        }

        return reportArray;
    }

    private static IList<IList<object?>> BuildSummaryReportArray(IList<JiraInitiative> allInitiativeData)
    {
        IList<IList<object?>> reportArray = new List<IList<object?>>();
        foreach (var initiative in allInitiativeData)
        {
            var row = new List<object?>
            {
                initiative.Description,
                initiative.InitiativeKey,
                initiative.Progress.Total,
                initiative.Progress.Done,
                initiative.Progress.Remaining,
                initiative.Progress.PercentDone,
                initiative.Status,
                initiative.Target?.ToString("d MMM yy")
            };
            reportArray.Add(row);
            foreach (var childPmPlan in initiative.PmPlans)
            {
                var childRow = new List<object?>
                {
                    childPmPlan.Description,
                    childPmPlan.PmPlanKey,
                    childPmPlan.Progress.Total,
                    childPmPlan.Progress.Done,
                    childPmPlan.Progress.Remaining,
                    childPmPlan.Progress.PercentDone,
                    childPmPlan.Status,
                    childPmPlan.Target?.ToString("d MMM yy")
                };
                reportArray.Add(childRow);
            }

            // Spacer empty row
            reportArray.Add(new List<object?>());
        }

        return reportArray;
    }

    private JiraIssue CreateJiraIssue(string? pmPlan, string? pmPlanSummary, dynamic issue)
    {
        string status = JiraFields.Status.Parse(issue) ?? Constants.Unknown;
        double storyPoints = JiraFields.StoryPoints.Parse(issue) ?? 0.0;

        return new JiraIssue(
            JiraFields.Key.Parse(issue)!,
            JiraFields.Created.Parse(issue),
            JiraFields.Resolved.Parse(issue),
            status,
            storyPoints,
            pmPlan ?? string.Empty,
            JiraFields.Summary.Parse(issue) ?? string.Empty,
            pmPlanSummary ?? string.Empty);
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
            var pmPlans = await runner.SearchJiraIssuesWithJqlAsync(string.Format(pmPlanJql, initiative), PmPlanFields);

            var allIssues = new List<JiraIssue>();
            foreach (var pmPlan in pmPlans)
            {
                string pmPlanKey = JiraFields.Key.Parse(pmPlan);
                string summary = JiraFields.Summary.Parse(pmPlan) ?? string.Empty;
                string status = JiraFields.Status.Parse(pmPlan) ?? Constants.Unknown;
                DateTimeOffset? target = JiraFields.ProjectTarget.Parse(pmPlan);
                var pmPlanData = new JiraPmPlan(pmPlanKey, summary, new StatLine(), status, target);
                var children = await runner.SearchJiraIssuesWithJqlAsync(string.Format(javPmKeyql, pmPlanKey), IssueFields);
                Console.WriteLine($"Fetched {children.Count} children for {pmPlan.key}");
                var range = children.Select<dynamic, JiraIssue>(i => CreateJiraIssue(pmPlanKey, summary, i)).ToList();
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
        var result = await runner.SearchJiraIssuesWithJqlAsync($"key={initiativeKey}", [JiraFields.Summary, JiraFields.Status, JiraFields.ProjectTarget]);
        var single = result.Single();
        string summary = JiraFields.Summary.Parse(single) ?? string.Empty;
        string status = JiraFields.Status.Parse(single) ?? Constants.Unknown;
        DateTimeOffset? target = JiraFields.ProjectTarget.Parse(single);
        return new JiraInitiative(initiativeKey, summary, new List<JiraPmPlan>(), new StatLine(), status, target);
    }

    private async Task<IReadOnlyList<string>> GetInitiativesForReport()
    {
        var list = await sheetReader.GetSheetNames();
        var initiatives = list.Where(x => x.StartsWith(ProductInitiativePrefix)).ToList();
        Console.WriteLine("Updating burn-up charts for the following Product Initiatives:");
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

    public record JiraIssue(
        string Key,
        DateTimeOffset CreatedDateTime,
        DateTimeOffset? ResolvedDateTime,
        string Status,
        double StoryPoints,
        string PmPlan,
        string Summary,
        string PmPlanSummary);
}
