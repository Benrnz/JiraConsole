using System.Text;

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
        JiraFields.CodeAreaParent,
        JiraFields.CodeArea
    ];

    private List<string> allCategories = new();
    private List<string> allCodeAreas = new();
    //private int dynamicIndex;

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

        await ExportBugStatsCodeAreas(jiras);
        await ExportBugStatsRecentDevelopment(jiras);
        await ExportBugStatsSeverities(jiras);
        await ExportBugStatsCategories(jiras);
    }

    private async Task ExportBugStatsRecentDevelopment(List<JiraIssue> jiras)
    {
        var currentMonth = CalculateStartDate();
        var bugCounts = new List<BarChartData>();
        do
        {
            var filteredList = jiras
                .Where(i => i.Created >= currentMonth && i.Created < currentMonth.AddMonths(1) && i.Resolution == Constants.CodeFixLast6 && i.BugType == Constants.BugTypeProduction)
                .ToList();
            // ReSharper disable InconsistentNaming
            var p1sTotal = filteredList.Count(i => i.Severity == Constants.SeverityCritical);
            var p2sTotal = filteredList.Count(i => i.Severity == Constants.SeverityMajor);
            // ReSharper restore InconsistentNaming
            var othersTotal = filteredList.Count - p1sTotal - p2sTotal;
            bugCounts.Add(new BarChartData(currentMonth, p1sTotal, p2sTotal, othersTotal));
            currentMonth = currentMonth.AddMonths(1);
        } while (currentMonth < DateTime.Today);

        var exporter = new SimpleCsvExporter(Key) { Mode = SimpleCsvExporter.FileNameMode.ExactName, OverrideSerialiseRecord = SerialiseToCsv };
        var fileName = exporter.Export(bugCounts, $"{Key}-RecentDev");

        var googleSheetUpdater = new GoogleSheetUpdater(fileName);
        await googleSheetUpdater.EditGoogleSheet("'RecentDev'!A1");
    }

    private async Task ExportBugStatsCategories(List<JiraIssue> jiras)
    {
        var currentMonth = CalculateStartDate();
        var bugCounts = new List<BarChartDataCategories>();
        this.allCategories = ParseCategories(jiras.Select(j => j.Category ?? Constants.Unknown).Distinct().ToList());
        do
        {
            var filteredList = jiras.Where(i => i.Created >= currentMonth && i.Created < currentMonth.AddMonths(1)).ToList();
            var categoryData = filteredList.GroupBy(j => j.Category ?? Constants.Unknown).ToList();
            var categoryCounts = new Dictionary<string, int>();
            foreach (var category in this.allCategories)
            {
                var matchingGroups = categoryData.Where(g => g.Key == category || g.Key.Contains(category));
                categoryCounts[category] = matchingGroups.Select(g => g.Count()).Sum();
            }

            bugCounts.Add(new BarChartDataCategories(currentMonth, categoryCounts));
            currentMonth = currentMonth.AddMonths(1);
        } while (currentMonth < DateTime.Today);

        var exporter = new SimpleCsvExporter(Key) { Mode = SimpleCsvExporter.FileNameMode.ExactName, OverrideSerialiseRecord = SerialiseToCsv, OverrideSerialiseHeader = SerialiseCatergoriesHeaderRow };
        var fileName = exporter.Export(bugCounts, $"{Key}-Categories");

        var googleSheetUpdater = new GoogleSheetUpdater(fileName);
        await googleSheetUpdater.EditGoogleSheet("'ProductCategories'!A1");
    }

    private string JoinCodeAreasParent(string? codeArea, string? codeAreaParent)
    {
        if (string.IsNullOrWhiteSpace(codeArea) && string.IsNullOrWhiteSpace(codeAreaParent))
        {
            return Constants.Unknown;
        }

        if (string.IsNullOrWhiteSpace(codeArea))
        {
            return codeAreaParent!;
        }

        if (string.IsNullOrWhiteSpace(codeAreaParent))
        {
            return codeArea!;
        }

        var myCodeArea = string.IsNullOrWhiteSpace(codeArea) ? Constants.Unknown : codeArea.Trim();
        var myCodeAreaParent = string.IsNullOrWhiteSpace(codeAreaParent) ? Constants.Unknown : codeAreaParent.Trim();

        return $"{myCodeArea} / {myCodeAreaParent}";
    }

    private async Task ExportBugStatsCodeAreas(List<JiraIssue> jiras)
    {
        var currentMonth = CalculateStartDate();
        var bugCounts = new List<BarChartDataCodeAreas>();
        this.allCodeAreas = ParseCodeAreas(jiras.Select(j => JoinCodeAreasParent(j.CodeArea, j.CodeAreaParent)).Distinct().ToList());
        do
        {
            var filteredList = jiras.Where(i => i.Created >= currentMonth && i.Created < currentMonth.AddMonths(1)).ToList();
            var areaCounts = new Dictionary<string, int>();
            foreach (var codeArea in this.allCodeAreas)
            {
                areaCounts[codeArea] = filteredList.Count(j => CodeAreaMatches(j.CodeArea, j.CodeAreaParent, codeArea));
            }

            bugCounts.Add(new BarChartDataCodeAreas(currentMonth, areaCounts));
            currentMonth = currentMonth.AddMonths(1);
        } while (currentMonth < DateTime.Today);

        var exporter = new SimpleCsvExporter(Key) { Mode = SimpleCsvExporter.FileNameMode.ExactName, OverrideSerialiseRecord = SerialiseToCsv, OverrideSerialiseHeader = SerialiseCodeAreasHeaderRow };
        var fileName = exporter.Export(bugCounts, $"{Key}-Areas");

        var googleSheetUpdater = new GoogleSheetUpdater(fileName);
        await googleSheetUpdater.EditGoogleSheet("'CodeAreas'!A1");
    }

    private bool CodeAreaMatches(string? codeArea, string? codeAreaParent, string matchThisCategory)
    {
        if (codeArea is not null && codeArea.Contains(matchThisCategory))
        {
            return true;
        }

        if (codeAreaParent is not null && codeAreaParent.Contains(matchThisCategory))
        {
            return true;
        }

        return false;
    }

    private string SerialiseCatergoriesHeaderRow()
    {
        return $"Month,{string.Join(',', this.allCategories)}";
    }

    private string SerialiseCodeAreasHeaderRow()
    {
        return $"Month,{string.Join(',', this.allCodeAreas)}";
    }

    private string SerialiseToCsv(object record)
    {
        if (record is BarChartDataCategories chartDataCategories)
        {
            var builder = new StringBuilder();
            builder.Append(chartDataCategories.Month.ToString("MMM-yy"));
            foreach (var category in this.allCategories)
            {
                builder.Append(',');
                builder.Append(chartDataCategories.CategoryData[category]);
            }

            return builder.ToString();
        }

        if (record is BarChartDataCodeAreas chartDataCodeAreas)
        {
            var builder = new StringBuilder();
            builder.Append(chartDataCodeAreas.Month.ToString("MMM-yy"));
            foreach (var category in this.allCodeAreas)
            {
                builder.Append(',');
                builder.Append(chartDataCodeAreas.CodeAreaData[category]);
            }

            return builder.ToString();
        }

        if (record is BarChartData chartDataSeverities)
        {
            var builder = new StringBuilder();
            builder.Append(chartDataSeverities.Month.ToString("MMM-yy"));
            builder.Append($",{chartDataSeverities.P1s},{chartDataSeverities.P2s},{chartDataSeverities.Others}");
            return builder.ToString();
        }

        throw new InvalidOperationException("Unsupported record type for CSV serialization.");
    }

    private List<string> ParseCategories(List<string> rawList)
    {
        // The rawList contains a list of categories, but some elements are comma seperated lists of categories. These need to be split and a new distinct list created.
        var categories = new HashSet<string>();
        foreach (var item in rawList)
        {
            var splitItems = item.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var splitItem in splitItems)
            {
                if (!string.IsNullOrWhiteSpace(splitItem))
                {
                    categories.Add(splitItem);
                }
            }
        }

        return categories.OrderBy(c => c).ToList();
    }

    private List<string> ParseCodeAreas(List<string> rawList)
    {
        // Parse the raw data in rawList and extract unique code areas. Some elements in the list may contain multiple code areas separated by commas or /.
        var codeAreas = new HashSet<string>();
        foreach (var item in rawList)
        {
            var splitItems = item.Split([',', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var splitItem in splitItems)
            {
                if (!string.IsNullOrWhiteSpace(splitItem))
                {
                    codeAreas.Add(splitItem);
                }
            }
        }

        return codeAreas.OrderBy(a => a).ToList();
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

        var exporter = new SimpleCsvExporter(Key) { Mode = SimpleCsvExporter.FileNameMode.ExactName, OverrideSerialiseRecord = SerialiseToCsv };
        var fileName = exporter.Export(bugCounts, $"{Key}-Severities");

        var googleSheetUpdater = new GoogleSheetUpdater(fileName);
        await googleSheetUpdater.EditGoogleSheet("'Severities'!A1");
    }

    private DateTime CalculateStartDate()
    {
        var thirteenMonthsAgo = DateTime.Now.AddMonths(-13);
        return new DateTime(thirteenMonthsAgo.Year, thirteenMonthsAgo.Month, 1);
    }

    private JiraIssue CreateJiraIssue(dynamic i)
    {
        //Console.Write(this.dynamicIndex++);
        //Console.Write(" ");
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
            JiraFields.Resolution.Parse<string?>(i),
            JiraFields.CodeAreaParent.Parse<string?>(i),
            JiraFields.CodeArea.Parse<string?>(i));
        return typedIssue;
    }

    // ReSharper disable InconsistentNaming
    private record BarChartData(DateTime Month, int P1s, int P2s, int Others);
    // ReSharper restore InconsistentNaming

    private record BarChartDataCategories(DateTime Month, IDictionary<string, int> CategoryData);

    private record BarChartDataCodeAreas(DateTime Month, IDictionary<string, int> CodeAreaData);

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
        string? Resolution,
        string? CodeAreaParent,
        string? CodeArea);
}
