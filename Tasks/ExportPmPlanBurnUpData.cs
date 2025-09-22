namespace BensJiraConsole.Tasks;

// ReSharper disable once UnusedType.Global
public class ExportPmPlanBurnUpData(IJiraQueryRunner runner, ICsvExporter exporter) : IJiraExportTask
{
    private const int LinearTrendWeeks = 4;
    private const string KeyString = "PMPLAN_BURNUPS";
    private static readonly DateTime ForecastCeilingDate = new(2026, 3, 31);

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

    public string Key => KeyString;
    public string Description => "Export PM Plan data for drawing a release _burn-up_charts_";

    public async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine(Description);
        var jqlPmPlans =
            $"IssueType = Idea AND \"PM Customer[Checkboxes]\"= Envest AND \"Required for Go-live[Checkbox]\" = 1 AND \"Estimation Status[Dropdown]\" = \"{Constants.HasDevTeamEstimate}\" ORDER BY Key";
        Console.WriteLine(jqlPmPlans);
        var childrenJql = "project=JAVPM AND (issue in (linkedIssues(\"{0}\")) OR parent in (linkedIssues(\"{0}\"))) ORDER BY key";
        Console.WriteLine($"ForEach PMPLAN: {childrenJql}");
        var pmPlans = await runner.SearchJiraIssuesWithJqlAsync(jqlPmPlans, PmPlanFields);

        var allIssues = new List<JiraIssue>();
        foreach (var pmPlan in pmPlans)
        {
            List<dynamic> children = await runner.SearchJiraIssuesWithJqlAsync(string.Format(childrenJql, pmPlan.key), IssueFields);
            Console.WriteLine($"Fetched {children.Count} children for {pmPlan.key}");
            var range = children.Select(c => new JiraIssue(
                JiraFields.Key.Parse(c),
                JiraFields.Created.Parse(c),
                JiraFields.Resolved.Parse(c),
                JiraFields.Status.Parse(c),
                JiraFields.StoryPoints.Parse(c),
                JiraFields.Key.Parse(pmPlan)));
            allIssues.AddRange(range);
        }

        Console.WriteLine($"Found {allIssues.Count} unique stories");

        var charts = ParseChartData(pmPlans.Select(p => (string)p.key), allIssues
            .OrderBy(i => i.PmPlan)
            .ThenBy(i => i.ResolvedDateTime)
            .ThenBy(i => i.CreatedDateTime));

        foreach (var pmPlan in charts.Keys)
        {
            var newChartData = AddTrendLines(charts[pmPlan].ToArray());
            charts[pmPlan] = newChartData;
        }

        ExportToCsv(charts);
    }

    private IEnumerable<BurnUpChartData> AddTrendLines(BurnUpChartData[] chartData)
    {
        var count = chartData.Length;
        if (count <= 2)
        {
            return chartData; // Not enough data points to calculate trend lines
        }

        var windowSize = count >= LinearTrendWeeks ? LinearTrendWeeks : count;
        var lastFourPoints = chartData.TakeLast(windowSize).ToList();
        if (lastFourPoints.Count < 2)
        {
            return chartData; // Not enough points to calculate a trend
        }

        var newChartData = chartData.ToList();

        var effortTrendIncrement = (lastFourPoints.Last().TotalDaysEffort - lastFourPoints.First().TotalDaysEffort) / windowSize;
        var workCompletedTrendIncrement = (lastFourPoints.Last().WorkCompleted - lastFourPoints.First().WorkCompleted) / windowSize;

        var runningTotalEffortTrendValue = lastFourPoints.First().TotalDaysEffort;
        var runningTotalWorkTrendValue = lastFourPoints.First().WorkCompleted;
        for (var i = 0; i < windowSize; i++)
        {
            lastFourPoints[i].TotalDaysEffortTrend = runningTotalEffortTrendValue;
            lastFourPoints[i].WorkCompletedTrend = runningTotalWorkTrendValue;
            runningTotalEffortTrendValue += effortTrendIncrement;
            runningTotalWorkTrendValue += workCompletedTrendIncrement;
        }

        var date = newChartData.Last().Date;
        var weeksDelta = (ForecastCeilingDate - date).Days / 7;
        if (weeksDelta > 1)
        {
            var newDataPoint = new BurnUpChartData
            {
                Date = ForecastCeilingDate,
                TotalDaysEffortTrend = newChartData.Last().TotalDaysEffortTrend + (effortTrendIncrement * weeksDelta),
                WorkCompletedTrend = newChartData.Last().WorkCompletedTrend + (workCompletedTrendIncrement * weeksDelta)
            };
            newChartData.Add(newDataPoint);
        }

        return newChartData;
    }

    private void ExportToCsv(IDictionary<string, IEnumerable<BurnUpChartData>> charts)
    {
        foreach (var pmPlan in charts.Keys)
        {
            exporter.SetFileNameMode(FileNameMode.ExactName, pmPlan);
            exporter.Export(charts[pmPlan]);
        }
    }

    private Dictionary<string, IEnumerable<BurnUpChartData>> ParseChartData(IEnumerable<string> pmPlans, IEnumerable<JiraIssue> rawData)
    {
        var results = new Dictionary<string, IEnumerable<BurnUpChartData>>();
        var rawDataCopy = rawData.ToList();
        foreach (var pmPlan in pmPlans)
        {
            var children = rawDataCopy.Where(i => i.PmPlan == pmPlan).ToList();
            if (!children.Any())
            {
                continue;
            }

            var chartData = new List<BurnUpChartData>();
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

                date = date.AddDays(7);
            }

            results.Add(pmPlan, chartData);
        }

        return results;
    }

    private record JiraIssue(string Key, DateTimeOffset CreatedDateTime, DateTimeOffset? ResolvedDateTime, string Status, double? StoryPoints, string PmPlan);
}
