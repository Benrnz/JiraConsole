namespace BensJiraConsole.Tasks;

public class InitiativeBurnUpsTask : IJiraExportTask
{
    private const string GoogleSheetId = "1OVUx08nBaD8uH-klNAzAtxFSKTOvAAk5Vnm11ALN0Zo";
    private const string TaskKey = "INIT_BURNUPS";
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
        JiraFields.EstimationStatus
    ];

    private readonly ICsvExporter exporter = new SimpleCsvExporter(TaskKey) { Mode = FileNameMode.ExactName };

    private readonly IJiraQueryRunner runner = new JiraQueryDynamicRunner();

    private readonly IWorkSheetReader sheetReader = new GoogleSheetReader(GoogleSheetId);

    private readonly IWorkSheetUpdater sheetUpdater = new GoogleSheetUpdater(GoogleSheetId);

    public string Description => "Export and update Initiative level PMPLAN data for drawing feature-set release _burn-up_charts_";

    public string Key => TaskKey;

    public async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine(Description);
        var initiativeKeys = await GetInitiativesForBurnUps();
        if (!initiativeKeys.Any())
        {
            Console.WriteLine("No Product Initiatives found to process. Exiting.");
            return;
        }

        var pmPlanJql = "issue in (linkedIssues(\"{0}\")) OR parent in (linkedIssues(\"{0}\")) AND \"Required for Go-live[Checkbox]\" = 1 ORDER BY key";
        var javPmKeyql = "project = JAVPM AND (issue in (linkedIssues(\"{0}\")) OR parent in (linkedIssues(\"{0}\"))) ORDER BY key";

        var allInitiativeData = new Dictionary<string, IEnumerable<BurnUpChartData>>();
        foreach (var initiative in initiativeKeys)
        {
            Console.WriteLine($"* Finding all work for {initiative}");
            var pmPlans = await this.runner.SearchJiraIssuesWithJqlAsync(string.Format(pmPlanJql, initiative), PmPlanFields);

            var allIssues = new List<JiraIssue>();
            foreach (var pmPlan in pmPlans)
            {
                List<dynamic> children = await this.runner.SearchJiraIssuesWithJqlAsync(string.Format(javPmKeyql, pmPlan.key), IssueFields);
                Console.WriteLine($"Fetched {children.Count} children for {pmPlan.key}");
                var range = children.Select<dynamic, JiraIssue>(i => CreateJiraIssue(initiative, i));
                allIssues.AddRange(range);
            }

            Console.WriteLine($"Found {allIssues.Count} unique stories");
            var chart = ParseChartData(allIssues
                .OrderBy(i => i.PmPlan)
                .ThenBy(i => i.ResolvedDateTime)
                .ThenBy(i => i.CreatedDateTime));
            allInitiativeData.Add(initiative, chart);
        } // For each initiative

        foreach (var initiative in initiativeKeys)
        {
            var fileName = this.exporter.Export(allInitiativeData[initiative], initiative);
            this.sheetUpdater.CsvFilePathAndName = fileName;
            await this.sheetUpdater.EditSheet($"'{initiative}'!A3", true);
            await this.sheetUpdater.ApplyDateFormat(initiative, 0, "d mmm yy");
        }
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

    private async Task<IReadOnlyList<string>> GetInitiativesForBurnUps()
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

    private IEnumerable<BurnUpChartData> ParseChartData(IEnumerable<JiraIssue> rawData)
    {
        const int dataPointPeriod = 7; // days / 1 week

        var children = rawData.ToList();
        var chartData = new List<BurnUpChartData>();

        if (!children.Any())
        {
            return chartData;
        }

        var date = DateUtils.FindBestStartDateForWeeklyData(children.Min(i => i.CreatedDateTime));

        while (date < DateTime.Today)
        {
            var dataPoint = new BurnUpChartData
            {
                Date = date.LocalDateTime,
                TotalDaysEffort = children
                    .Where(i => i.CreatedDateTime < date).Sum(i => i.StoryPoints),
                WorkCompleted = children
                    .Where(i => i.ResolvedDateTime < date && i.Status == Constants.DoneStatus)
                    .Sum(i => i.StoryPoints)
            };
            if (dataPoint.TotalDaysEffort + dataPoint.WorkCompleted > 0)
            {
                chartData.Add(dataPoint);
            }

            date = date.AddDays(dataPointPeriod);
        }

        // Add four more periods to leave some forecasting space on the right of the chart.
        var maxDate = chartData.Max(c => c.Date);
        for (var i = 0; i < 4; i++)
        {
            maxDate = maxDate.AddDays(dataPointPeriod);
            chartData.Add(new BurnUpChartData
            {
                Date = maxDate,
                TotalDaysEffort = null, //Must be null to stop the line from crashing to x-axis.
                WorkCompleted = null
            });
        }

        return chartData;
    }

    private record JiraIssue(string Key, DateTimeOffset CreatedDateTime, DateTimeOffset? ResolvedDateTime, string Status, double StoryPoints, string PmPlan);
}
