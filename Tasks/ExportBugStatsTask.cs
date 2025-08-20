namespace BensJiraConsole.Tasks;

public class ExportBugStatsTask : IJiraExportTask
{
    private static readonly FieldMapping[] Fields =
    [
        JiraFields.Summary,
        JiraFields.Created,
        JiraFields.Status,
        JiraFields.Category,
        JiraFields.Severity,
        JiraFields.Team,
        JiraFields.BugType,
        JiraFields.StoryPoints,
        JiraFields.DevTimeSpent,
        JiraFields.Resolved,
        JiraFields.Resolution
    ];

    private int dynamicIndex;

    public string Key => "BUG_STATS";
    public string Description => "Export a series of exports summarising bug statistics for JAVPM.";

    public async Task ExecuteAsync(string[] fields)
    {
        Console.WriteLine(Description);
        var dynamicRunner = new JiraQueryDynamicRunner();
        var jql = """project = JAVPM AND issuetype = Bug AND "Bug Type[Dropdown]" IN (Production, UAT) AND created >= startOfMonth("-13M")""";
        Console.WriteLine(jql);
        Console.WriteLine();
        var jiras = (await dynamicRunner.SearchJiraIssuesWithJqlAsync(jql, Fields))
            .Select(CreateJiraIssue)
            .OrderBy(i => i.Created)
            .ToList();

        await ExportBugStatsSeverities(jiras);
        await ExportBugStatsCategories(jiras);

    }

    private async Task ExportBugStatsCategories(List<JiraIssue> jiras)
    {
        var currentMonth = CalculateStartDate();
        var bugCounts = new List<BarChartData>();
        // do
        // {
        //     var filteredList = jiras.Where(i => i.Created >= currentMonth && i.Created < currentMonth.AddMonths(1)).ToList();
        //     //Maybe create a dictionary to hold counts by category?
        //     bugCounts.Add(new BarChartData(currentMonth, p1sTotal, p2sTotal, othersTotal));
        //     currentMonth = currentMonth.AddMonths(1);
        // } while (currentMonth < DateTime.Today);
    }

    private async Task ExportBugStatsSeverities(List<JiraIssue> jiras)
    {
        var currentMonth = CalculateStartDate();
        var bugCounts = new List<BarChartData>();
        do
        {
            var filteredList = jiras.Where(i => i.Created >= currentMonth && i.Created < currentMonth.AddMonths(1)).ToList();
            // ReSharper disable InconsistentNaming
            var p1sTotal = filteredList.Count(i => i.Severity == Constants.SeverityCritical);
            var p2sTotal = filteredList.Count(i => i.Severity == Constants.SeverityMajor);
            // ReSharper restore InconsistentNaming
            var othersTotal = filteredList.Count - p1sTotal - p2sTotal;
            bugCounts.Add(new BarChartData(currentMonth, p1sTotal, p2sTotal, othersTotal));
            currentMonth = currentMonth.AddMonths(1);
        } while (currentMonth < DateTime.Today);

        var exporter = new SimpleCsvExporter(Key) { Mode = SimpleCsvExporter.FileNameMode.ExactName };
        var fileName = exporter.Export(bugCounts, Key);

        var googleExporter = new GoogleDriveUploader();
        await googleExporter.UploadCsvAsync(fileName, $"{Key}-Severities.csv");
    }

    private DateTime CalculateStartDate()
    {
        var thirteenMonthsAgo = DateTime.Now.AddMonths(-13);
        return new DateTime(thirteenMonthsAgo.Year, thirteenMonthsAgo.Month, 1);
    }

    private JiraIssue CreateJiraIssue(dynamic i)
    {
        Console.Write(this.dynamicIndex++);
        Console.Write(" ");
        var typedIssue = new JiraIssue(
            JiraFields.Key.Parse<string>(i),
            JiraFields.Summary.Parse<string>(i),
            JiraFields.Created.Parse<DateTimeOffset>(i),
            JiraFields.Resolved.Parse<DateTimeOffset?>(i),
            JiraFields.Status.Parse<string>(i),
            JiraFields.Category.Parse<string?>(i),
            JiraFields.Severity.Parse<string?>(i),
            JiraFields.Team.Parse<string?>(i),
            JiraFields.BugType.Parse<string>(i),
            JiraFields.StoryPoints.Parse<double?>(i),
            JiraFields.DevTimeSpent.Parse<string?>(i),
            JiraFields.Resolution.Parse<string?>(i));
        return typedIssue;

        // Old way of creating typed issues, kept for reference.  Very prone to errors with dynamic no-intellisense property names.
        // return new JiraIssue(
        //     (string)i.key,
        //     (string)i.Summary,
        //     (DateTimeOffset)i.Created,
        //     (DateTimeOffset?)i.Resolved,
        //     (string)i.Status,
        //     (string?)i.Category,
        //     (string?)i.Severity,
        //     (string?)i.Team,
        //     (string)i.BugType,
        //     (double?)i.StoryPoints,
        //     devTimeSpent,
        //     (string?)i.Resolution);
    }

    // ReSharper disable InconsistentNaming
    private record BarChartData(DateTime Month, int P1s, int P2s, int Others);
    // ReSharper restore InconsistentNaming

    private record JiraIssue(
        string Key,
        string Summary,
        DateTimeOffset Created,
        DateTimeOffset? Resolved,
        string Status,
        string? Category,
        string? Severity,
        string? Team,
        string BugType,
        double? StoryPoints,
        string? DevTimeSpent,
        string? Resolution);
}
