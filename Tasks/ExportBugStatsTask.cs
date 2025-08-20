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
        JiraFields.Resolution,
    ];

    public string Key => "BUG_STATS";
    public string Description => "Export a series of exports summarising bug statistics for JAVPM.";

    public async Task ExecuteAsync(string[] fields)
    {
        Console.WriteLine(Description);
        var dynamicRunner = new JiraQueryDynamicRunner();
        var jql = """project = JAVPM AND issuetype = Bug AND "Bug Type[Dropdown]" IN (Production, UAT) AND created >= startOfMonth("-13M")""";
        var jiras = (await dynamicRunner.SearchJiraIssuesWithJqlAsync(jql, Fields))
            .Select(CreateJiraIssue)
            .OrderBy(i => i.Created)
            .ToList();

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

        var exporter = new SimpleCsvExporter(Key);
        exporter.Export(bugCounts);
    }

    private DateTime CalculateStartDate()
    {
        var thirteenMonthsAgo = DateTime.Now.AddMonths(-13);
        return new DateTime(thirteenMonthsAgo.Year, thirteenMonthsAgo.Month, 1);
    }

    private int dynamicIndex = 0;
    private JiraIssue CreateJiraIssue(dynamic i)
    {
        Console.WriteLine(this.dynamicIndex++);
        string? devTimeSpent = null;
        // DevTimeSpent has come through as a DateTimeOffset in real data and also a string.
        if (i.DevTimeSpent is string stringTime)
        {
            devTimeSpent = stringTime;
        } else if (i.DevTimeSpent is DateTimeOffset dateTime)
        {
            devTimeSpent = dateTime.ToString("d");
        }
        return new JiraIssue(
            Key: (string)i.key,
            Summary: (string)i.Summary,
            Created: (DateTimeOffset)i.Created,
            Resolved: (DateTimeOffset?)i.Resolved,
            Status: (string)i.Status,
            Category: (string?)i.Category,
            Severity: (string?)i.Severity,
            Team: (string?)i.Team,
            BugType: (string)i.BugType,
            StoryPoints: (double?)i.StoryPoints,
            DevTimeSpent: devTimeSpent,
            Resolution: (string?)i.Resolution);
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
