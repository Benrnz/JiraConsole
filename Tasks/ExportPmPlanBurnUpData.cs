namespace BensJiraConsole.Tasks;

// ReSharper disable once UnusedType.Global
public class ExportPmPlanBurnUpData : IJiraExportTask
{
    private const int LinerTrendWeeks = 4;
    private static readonly DateTime ForecastCeilingDate = new(2026, 3, 31);

    public FieldMapping[] IssueFields =>
    [
        //  JIRA Field Name,          Friendly Alias,                    Flatten object field name
        new("summary", "Summary"),
        new("status", "Status", "name"),
        new("parent", "Parent", "key"),
        new("customfield_10004", "StoryPoints"),
        new("timeoriginalestimate", "Original Estimate"),
        new("created"),
        new("resolutiondate", "Resolved")
    ];

    public FieldMapping[] PmPlanFields =>
    [
        //  JIRA Field Name,          Friendly Alias,                    Flatten object field name
        new("summary", "Summary"),
        new("status", "Status", "name"),
        new("issuetype", "IssueType", "name"),
        new("customfield_12038", "PmPlanHighLevelEstimate"),
        new("customfield_12137", "EstimationStatus", "value"),
        new("customfield_11986", "IsReqdForGoLive")
    ];

    public string Key => "PMPLAN_BURNUP";
    public string Description => "Export PM Plan data for drawing a release burn-up chart";

    public async Task ExecuteAsync(string[] fields)
    {
        Console.WriteLine(Description);
        var jqlPmPlans = $"IssueType = Idea AND \"PM Customer[Checkboxes]\"= Envest AND \"Required for Go-live[Checkbox]\" = 1 AND \"Estimation Status[Dropdown]\" = \"{Constants.HasDevTeamEstimate}\" ORDER BY Key";
        Console.WriteLine(jqlPmPlans);
        var childrenJql = "project=JAVPM AND (issue in (linkedIssues(\"{0}\")) OR parent in (linkedIssues(\"{0}\"))) ORDER BY key";
        Console.WriteLine($"ForEach PMPLAN: {childrenJql}");
        var dynamicRunner = new JiraQueryDynamicRunner();
        var pmPlans = await dynamicRunner.SearchJiraIssuesWithJqlAsync(jqlPmPlans, PmPlanFields);

        var allIssues = new List<JiraIssue>();
        foreach (var pmPlan in pmPlans)
        {
            List<dynamic> children = await dynamicRunner.SearchJiraIssuesWithJqlAsync(string.Format(childrenJql, pmPlan.key), IssueFields);
            Console.WriteLine($"Fetched {children.Count} children for {pmPlan.key}");
            var range = children.Select(c => new JiraIssue(
                (string)c.key,
                (DateTimeOffset)c.created,
                (DateTimeOffset?)c.Resolved,
                (string)c.Status,
                (double?)c.StoryPoints,
                (string)pmPlan.key));
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

        var startIdx = count > LinerTrendWeeks ? count - LinerTrendWeeks + 1 : 0;
        var windowSize = count - startIdx + 1;
        var lastSixPoints = chartData.Skip(startIdx - 1).Take(windowSize).ToList();
        if (lastSixPoints.Count < 2 || windowSize < 2)
        {
            return chartData; // Not enough points to calculate a trend
        }

        var newChartData = chartData.ToList();

        var effortTrendIncrement = (lastSixPoints.Last().TotalDaysEffort - lastSixPoints.First().TotalDaysEffort) / (windowSize - 1);
        var workCompletedTrendIncrement = (lastSixPoints.Last().WorkCompleted - lastSixPoints.First().WorkCompleted) / (windowSize - 1);

        var runningTotalEffortTrendValue = lastSixPoints.First().TotalDaysEffort;
        var runningTotalWorkTrendValue = lastSixPoints.First().WorkCompleted;
        for (var i = 0; i < windowSize; i++)
        {
            lastSixPoints[i].TotalDaysEffortTrend = runningTotalEffortTrendValue;
            lastSixPoints[i].WorkCompletedTrend = runningTotalWorkTrendValue;
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
            var exporter = new SimpleCsvExporter(Key) { Mode = SimpleCsvExporter.FileNameMode.ExactName };
            exporter.Export(charts[pmPlan], pmPlan);
        }
    }

    private Dictionary<string, IEnumerable<BurnUpChartData>> ParseChartData(IEnumerable<string> pmPlans, IEnumerable<JiraIssue> rawData)
    {
        var results = new Dictionary<string, IEnumerable<BurnUpChartData>>();
        foreach (var pmPlan in pmPlans)
        {
            var children = rawData.Where(i => i.PmPlan == pmPlan).ToList();
            if (!children.Any())
            {
                continue;
            }

            var chartData = new List<BurnUpChartData>();
            var date = DateUtils.FindBestStartDate(children.Min(i => i.CreatedDateTime));

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
