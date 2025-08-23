namespace BensJiraConsole.Tasks;

public class ExportWholesaleBrokersBurnupData : IJiraExportTask
{
    private const int LinerTrendWeeks = 4;
    private static readonly DateTime ForecastCeilingDate = new(2026, 3, 31);

    private static readonly FieldMapping[] EpicFields =
    [
        JiraFields.IssueType,
        JiraFields.Status,
        JiraFields.StoryPoints,
        JiraFields.Created,
        JiraFields.Resolution,
        JiraFields.Resolved
    ];

    private static readonly FieldMapping[] IssueFields =
    [
        JiraFields.Summary,
        JiraFields.Status,
        JiraFields.ParentKey,
        JiraFields.StoryPoints,
        JiraFields.OriginalEstimate,
        JiraFields.Created,
        JiraFields.Resolved
    ];

    private readonly IList<JiraIssue> resultList = new List<JiraIssue>();

    public string Key => "WB_BURNUP";

    public string Description => "Export Wholesale Brokers data for drawing a release burn-up chart";

    public async Task ExecuteAsync(string[] fields)
    {
        Console.WriteLine(Description);
        var dynamicRunner = new JiraQueryDynamicRunner();

        var keys = await RetrieveEpics(dynamicRunner);
        if (keys is not null)
        {
            await RetrieveChildrenOfEpics(keys, dynamicRunner);
        }

        // Retrieve PMPLANs tagged as Wholesale Brokers
        var pmPlanKeys = await RetrievePmPlans(dynamicRunner);
        if (pmPlanKeys is not null && pmPlanKeys.Length > 0)
        {
            // Retrieve children of these PMPLANs
            await RetrieveChildrenOfPmPlans(pmPlanKeys, dynamicRunner);
        }

        var chartData = CreateBurnUpChartData();

        var fileName = ExportCsvFiles(chartData);
        //await SaveToGoogleDrive(fileName);
    }

    private async Task SaveToGoogleDrive(string fileName)
    {
        var googleUploader = new GoogleDriveUploader();
        await googleUploader.UploadCsvAsync(fileName, $"{Key}.csv");
    }

    private string ExportCsvFiles(BurnUpChartData[] chartData)
    {
        var exporter = new SimpleCsvExporter(Key) { Mode = SimpleCsvExporter.FileNameMode.ExactName };
        var fileName = exporter.Export(chartData, Key);
        exporter.Export(this.resultList, Key + "_Issues");
        return fileName;
    }

    private BurnUpChartData[] CreateBurnUpChartData()
    {
        // Remove duplicates
        var distinctIssues = this.resultList
            .GroupBy(i => i.Key)
            .Select(g => g.First())
            .ToList();

        var chartData = ParseChartData(distinctIssues);
        chartData = AddTrendLines(chartData);
        return chartData;
    }

    private async Task RetrieveChildrenOfPmPlans(string[] keys, JiraQueryDynamicRunner dynamicRunner)
    {
        var childrenJql = "project=JAVPM AND (issue in (linkedIssues(\"{0}\")) OR parent in (linkedIssues(\"{0}\"))) ORDER BY key";
        Console.WriteLine($"ForEach PMPLAN: {childrenJql}");

        var issueCount = 0;
        foreach (var pmPlan in keys)
        {
            var allIssues = await dynamicRunner.SearchJiraIssuesWithJqlAsync(string.Format(childrenJql, pmPlan), IssueFields);
            allIssues.ForEach(i =>
            {
                this.resultList.Add(CreateJiraIssueFromDynamic(i, "Child of PMPLAN"));
                issueCount++;
            });
        }

        Console.WriteLine($"Found {issueCount} issues");
    }

    private async Task RetrieveChildrenOfEpics(string keys, JiraQueryDynamicRunner dynamicRunner)
    {
        var childrenJql = $"project = JAVPM AND \"Epic Link\" IN ({keys}) ORDER BY created ASC";
        Console.WriteLine($"ForEach Epic: {childrenJql}");
        var allIssues = await dynamicRunner.SearchJiraIssuesWithJqlAsync(childrenJql, IssueFields);
        Console.WriteLine($"Found {allIssues.Count} issues");

        allIssues.Select(i => CreateJiraIssueFromDynamic(i, "Child of labelled epic"))
            .ToList()
            .ForEach(i => this.resultList.Add(i));
    }

    private static JiraIssue CreateJiraIssueFromDynamic(dynamic i, string source)
    {
        DateTimeOffset? resolvedDate = null;
        if (i.Resolved is DateTimeOffset dateTime)
        {
            resolvedDate = dateTime;
        }

        return new JiraIssue(
            JiraFields.Key.Parse<string>(i),
            JiraFields.Created.Parse<DateTimeOffset>(i),
            JiraFields.Resolved.Parse<DateTimeOffset>(i),
            JiraFields.Status.Parse<string>(i),
            JiraFields.StoryPoints.Parse<double?>(i),
            source);
    }

    private async Task<string?> RetrieveEpics(JiraQueryDynamicRunner dynamicRunner)
    {
        var jqlEpics = "project = JAVPM AND labels = Wholesale_Broker order by created DESC";
        Console.WriteLine(jqlEpics);

        var epics = await dynamicRunner.SearchJiraIssuesWithJqlAsync(jqlEpics, EpicFields);
        if (!epics.Any())
        {
            Console.WriteLine("No Wholesale Brokers epics found.");
            return null;
        }

        if (epics.Any(e => e.IssueType != Constants.EpicType && e.IssueType is not null))
        {
            foreach (var issue in epics.Where(e => e.IssueType != Constants.EpicType))
            {
                this.resultList.Add(CreateJiraIssueFromDynamic(issue, "Directly labelled issue"));
            }
        }

        Console.WriteLine($"Found {epics.Count} epics. Found {this.resultList.Count} other issues.");

        return string.Join(", ", epics.Select(x => x.key).ToArray());
    }

    private async Task<string[]?> RetrievePmPlans(JiraQueryDynamicRunner dynamicRunner)
    {
        var jqlPmPlans = "project = \"Product Planning\" \nAND \"PM Customer[Checkboxes]\" = \"Wholesale Brokers\"";
        Console.WriteLine(jqlPmPlans);

        var pmPlans = await dynamicRunner.SearchJiraIssuesWithJqlAsync(jqlPmPlans, []);
        if (!pmPlans.Any())
        {
            Console.WriteLine("No Wholesale Brokers PMPLANs found.");
            return null;
        }

        return pmPlans.Select(x => (string)x.key).ToArray();
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

    private BurnUpChartData[] ParseChartData(IEnumerable<JiraIssue> rawData)
    {
        var results = new List<BurnUpChartData>();
        var children = rawData.ToList();

        if (!children.Any())
        {
            return results.ToArray();
        }

        var date = DateUtils.FindBestStartDateForWeeklyData(children.Min(i => i.CreatedDateTime));

        while (date <= DateTime.Today)
        {
            var dataPoint = new BurnUpChartData
            {
                Date = date.LocalDateTime,
                TotalDaysEffort = children
                    .Where(i => i.CreatedDateTime <= date).Sum(i => i.StoryPoints),
                WorkCompleted = children
                    .Where(i => i.ResolvedDateTime <= date && i.Status == Constants.DoneStatus)
                    .Sum(i => i.StoryPoints)
            };
            if (dataPoint.TotalDaysEffort + dataPoint.WorkCompleted > 0)
            {
                results.Add(dataPoint);
            }

            date = date.AddDays(7);
        }

        return results.ToArray();
    }

    private record JiraIssue(string Key, DateTimeOffset CreatedDateTime, DateTimeOffset? ResolvedDateTime, string Status, double? StoryPoints, string Source);
}
