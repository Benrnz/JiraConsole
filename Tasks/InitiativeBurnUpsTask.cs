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

        var task = new InitiativeProgressTableTask();
        await task.LoadData();

        var initiativeKeys = task.AllIssuesData.Keys;
        foreach (var initiative in initiativeKeys)
        {
            var issues = task.AllIssuesData[initiative];
            var chart = ParseChartData(issues
                .OrderBy(i => i.PmPlan)
                .ThenBy(i => i.ResolvedDateTime)
                .ThenBy(i => i.CreatedDateTime));
            var fileName = this.exporter.Export(chart, initiative);
            this.sheetUpdater.CsvFilePathAndName = fileName;
            await this.sheetUpdater.EditSheet($"'{initiative}'!A3", true);
            await this.sheetUpdater.ApplyDateFormat(initiative, 0, "d mmm yy");
        }
    }

    private IEnumerable<BurnUpChartData> ParseChartData(IEnumerable<InitiativeProgressTableTask.JiraIssue> rawData)
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
}
