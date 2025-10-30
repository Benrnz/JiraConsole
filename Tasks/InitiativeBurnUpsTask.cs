namespace BensJiraConsole.Tasks;

public class InitiativeBurnUpsTask(ICsvExporter exporter, IWorkSheetUpdater sheetUpdater, InitiativeProgressTableTask tableTask) : IJiraExportTask
{
    private const string GoogleSheetId = "1OVUx08nBaD8uH-klNAzAtxFSKTOvAAk5Vnm11ALN0Zo";
    private const string TaskKey = "INIT_BURNUPS";

    public string Description => "Export and update Initiative level PMPLAN data for drawing feature-set release _burn-up_charts_";

    public string Key => TaskKey;

    public async Task ExecuteAsync(string[] args)
    {
        await tableTask.LoadData();
        await ExecuteAsync(tableTask, args);
    }

    public async Task ExecuteAsync(InitiativeProgressTableTask mainTask, string[] args)
    {
        Console.WriteLine(Description);

        await sheetUpdater.Open(GoogleSheetId);
        var initiativeKeys = mainTask.AllIssuesData.Keys;
        foreach (var initiative in initiativeKeys)
        {
            var issues = mainTask.AllIssuesData[initiative];
            var chart = ParseChartData(issues
                    .OrderBy(i => i.PmPlan)
                    .ThenBy(i => i.ResolvedDateTime)
                    .ThenBy(i => i.CreatedDateTime))
                .ToList();
            exporter.SetFileNameMode(FileNameMode.ExactName, initiative);
            var fileName = exporter.Export(chart);
            sheetUpdater.CsvFilePathAndName = fileName;

            // Update Header
            var initiativeRecord = mainTask.AllInitiativesData.Single(i => i.InitiativeKey == initiative);
            sheetUpdater.EditSheet($"'{initiative}'!A1", new List<IList<object?>>([[$"{initiativeRecord.InitiativeKey} {initiativeRecord.Description}"]]));

            // Update data table
            if (chart.Any())
            {
                await sheetUpdater.ImportFile($"'{initiative}'!A3", true);
                sheetUpdater.ApplyDateFormat(initiative, 0, "d mmm yy");
                var children = mainTask.AllIssuesData[initiative]
                    .Where(i => i.Status != Constants.DoneStatus)
                    .GroupBy(i => i.PmPlan)
                    .SelectMany(g => g)
                    .OrderBy(x => x.PmPlan)
                    .ThenBy(x => x.Key);
                var childrenArray = new List<IList<object?>>();
                var currentPmPlan = string.Empty;
                foreach (var child in children)
                {
                    if (currentPmPlan != child.PmPlan)
                    {
                        // new PmPlan header row
                        var header = new List<object?> { child.PmPlan, child.PmPlanSummary };
                        childrenArray.Add(header);
                        currentPmPlan = child.PmPlan;
                    }

                    var row = new List<object?> { null, child.Key, child.Summary, null, null, null, child.StoryPoints };
                    childrenArray.Add(row);
                }

                sheetUpdater.ClearRange($"{initiative}", "F43:L1000");
                sheetUpdater.EditSheet($"'{initiative}'!G43", childrenArray, true);
            }
        }

        await sheetUpdater.SubmitBatch();
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
        var maxDate = chartData.Any() ? chartData.Max(c => c.Date) : DateTime.Today;
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
