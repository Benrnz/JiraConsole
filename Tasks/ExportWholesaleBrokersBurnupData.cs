namespace BensJiraConsole.Tasks;

public class ExportWholesaleBrokersBurnupData : IJiraExportTask
{
    private const int LinerTrendWeeks = 4;
    private static readonly DateTime ForecastCeilingDate = new(2026, 3, 31);

    private readonly FieldMapping[] EpicFields = [];

    private readonly FieldMapping[] IssueFields =
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

    public string Key => "WB_BURNUP";

    public string Description => "Export Wholesale Brokers data for drawing a release burn-up chart";

    public async Task ExecuteAsync(string[] fields)
    {
        Console.WriteLine(Description);
        var jqlEpics = "project = JAVPM AND labels = Wholesale_Broker order by created DESC";
        Console.WriteLine(jqlEpics);

        var dynamicRunner = new JiraQueryDynamicRunner();
        var epics = await dynamicRunner.SearchJiraIssuesWithJqlAsync(jqlEpics, this.EpicFields);
        if (!epics.Any())
        {
            Console.WriteLine("No Wholesale Brokers epics found.");
            return;
        }

        Console.WriteLine($"Found {epics.Count} epics");

        var keys = string.Join(", ", epics.Select(x => x.key).ToArray());


        var childrenJql = $"project = JAVPM AND \"Epic Link\" IN ({keys}) ORDER BY created DESC";
        Console.WriteLine($"ForEach Epic: {childrenJql}");
        var allIssues = await dynamicRunner.SearchJiraIssuesWithJqlAsync(childrenJql, this.IssueFields);

        Console.WriteLine($"Found {allIssues.Count} issues");

        var jiraIssues = allIssues.Select(i => new JiraIssue(
            (string)i.key,
            (DateTime)i.created.UtcDateTime,
            (DateTime?)i.Resolved?.UtcDateTime,
            (string)i.Status,
            (double?)i.StoryPoints));

        var chartData = ParseChartData(jiraIssues).ToArray();
        chartData = AddTrendLines(chartData);

        var exporter = new SimpleCsvExporter(Key) { Mode = SimpleCsvExporter.FileNameMode.ExactName };
        exporter.Export(chartData, Key);
    }

    private BurnUpChartData[] AddTrendLines(BurnUpChartData[] chartData)
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

        return newChartData.ToArray();
    }

    private IEnumerable<BurnUpChartData> ParseChartData(IEnumerable<JiraIssue> rawData)
    {
        var results = new List<BurnUpChartData>();
        var children = rawData.ToList();

        if (!children.Any())
        {
            return results;
        }

        var date = children.Min(i => i.CreatedDateTime);
        if (date == DateTime.MinValue)
        {
            return results;
        }

        while (date <= DateTime.Today)
        {
            var dataPoint = new BurnUpChartData
            {
                Date = date,
                TotalDaysEffort = children
                    .Where(i => i.CreatedDateTime <= date).Sum(i => i.StoryPoints),
                WorkCompleted = children
                    .Where(i => i.ResolvedDateTime <= date && i.Status == "Done")
                    .Sum(i => i.StoryPoints)
            };
            if (dataPoint.TotalDaysEffort + dataPoint.WorkCompleted > 0)
            {
                results.Add(dataPoint);
            }

            date = date.AddDays(7);
        }

        return results;
    }

    private record JiraIssue(string Key, DateTime CreatedDateTime, DateTime? ResolvedDateTime, string Status, double? StoryPoints);
}
