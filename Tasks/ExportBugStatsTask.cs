using System.Text;

namespace BensJiraConsole.Tasks;

public class ExportBugStatsTask(IJiraQueryRunner runner, ICsvExporter exporter, IWorkSheetUpdater sheetUpdater) : IJiraExportTask
{
    // JAVPM Bug Analysis
    private const string GoogleSheetId = "16bZeQEPobWcpsD8w7cI2ftdSoT1xWJS8eu41JTJP-oI";
    private const string KeyString = "BUG_STATS";

    private static readonly IFieldMapping[] Fields =
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
        JiraFields.CodeArea,
        JiraFields.CustomersMultiSelect,
    ];

    private List<string> allCategories = new();
    private List<string> allCodeAreas = new();
    //private int dynamicIndex;

    public string Key => KeyString;
    public string Description => "Export a series of exports summarising _bug_stats_ for JAVPM.";

    public async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine(Description);
        var jql = """project = JAVPM AND issuetype = Bug AND "Bug Type[Dropdown]" IN (Production, UAT) AND created >= startOfMonth("-13M")""";
        Console.WriteLine(jql);
        Console.WriteLine();
        var jiras = (await runner.SearchJiraIssuesWithJqlAsync(jql, Fields))
            .Select(CreateJiraIssue)
            .OrderBy(i => i.Created)
            .ToList();

        await sheetUpdater.Open(GoogleSheetId);
        await ExportBugStatsCodeAreas(jiras);
        await ExportBugStatsRecentDevelopment(jiras);
        var monthTotalsForSeverities = await ExportBugStatsSeverities(jiras);
        await ExportBugStatsReportedVsResolved(jiras, monthTotalsForSeverities);
        await ExportBugStatsEnvestSeverities(jiras, monthTotalsForSeverities);
        await ExportBugStatsCategories(jiras);
    }

    private DateTime CalculateStartDate()
    {
        var thirteenMonthsAgo = DateTime.Now.AddMonths(-13);
        return new DateTime(thirteenMonthsAgo.Year, thirteenMonthsAgo.Month, 1);
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

    private JiraIssue CreateJiraIssue(dynamic i)
    {
        //Console.Write(this.dynamicIndex++);
        //Console.Write(" ");
        var typedIssue = new JiraIssue(
            JiraFields.Key.Parse(i),
            JiraFields.Summary.Parse(i),
            JiraFields.Created.Parse(i),
            JiraFields.Resolved.Parse(i),
            JiraFields.Status.Parse(i),
            JiraFields.Category.Parse(i),
            JiraFields.Severity.Parse(i),
            JiraFields.Team.Parse(i),
            JiraFields.BugType.Parse(i),
            JiraFields.StoryPoints.Parse(i),
            JiraFields.DevTimeSpent.Parse(i),
            JiraFields.Resolution.Parse(i),
            JiraFields.CodeAreaParent.Parse(i),
            JiraFields.CodeArea.Parse(i),
            JiraFields.CustomersMultiSelect.Parse(i) ?? string.Empty);
        return typedIssue;
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

        exporter.SetFileNameMode(FileNameMode.ExactName, $"{Key}-Categories");
        var fileName = exporter.Export(bugCounts, SerialiseCatergoriesHeaderRow, SerialiseToCsv);

        sheetUpdater.CsvFilePathAndName = fileName;
        await sheetUpdater.EditSheet("'ProductCategories'!A1");
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

        exporter.SetFileNameMode(FileNameMode.ExactName, $"{Key}-Areas");
        var fileName = exporter.Export(bugCounts, SerialiseCodeAreasHeaderRow, SerialiseToCsv);

        sheetUpdater.CsvFilePathAndName = fileName;
        await sheetUpdater.EditSheet("'CodeAreas'!A1");
    }

    private async Task ExportBugStatsReportedVsResolved(List<JiraIssue> jiras, List<BarChartData> severityTotals)
    {
        var chartData = new List<LayeredBarChartData>();
        var jql = """project = JAVPM AND issuetype = Bug AND "Bug Type[Dropdown]" IN (Production, UAT) AND status != Done""";
        var issuesBacklog = (await runner.SearchJiraIssuesWithJqlAsync(jql, Fields)).Select(CreateJiraIssue).ToList();
        foreach (var totalChartDataMonth in severityTotals)
        {
            var month = totalChartDataMonth.Month;
            var monthIssueBacklog = issuesBacklog.Where(x => x.Created < month.AddMonths(1));
            var p1Backlog = monthIssueBacklog.Where(x => x.Severity == Constants.SeverityCritical).Count();
            var p2Backlog = monthIssueBacklog.Where(x => x.Severity == Constants.SeverityMajor).Count();
            var otherBacklog = monthIssueBacklog.Count() - p1Backlog - p2Backlog;
            var monthbacklogData = new BarChartData(month, p1Backlog, p2Backlog, otherBacklog);
            var row = new LayeredBarChartData(totalChartDataMonth, monthbacklogData);
            chartData.Add(row);
        }

        exporter.SetFileNameMode(FileNameMode.ExactName, $"{Key}-ReportVsBacklog");

        var fileName = exporter.Export(chartData, () => "Month,New Bugs Reported,,,Ticket Backlog\n,P1,P2,Other,Open P1s,Open P2s,Open Others", SerialiseToCsv);
        sheetUpdater.CsvFilePathAndName = fileName;
        await sheetUpdater.EditSheet("'Reported Vs Backlog'!A1");
    }

        private async Task ExportBugStatsEnvestSeverities(List<JiraIssue> jiras, List<BarChartData> severityTotals)
    {
        var chartData = new List<LayeredBarChartData>();
        var envestChartData = await ExportBugStatsSeverities(jiras, Constants.Envest);
        foreach (var totalChartDataMonth in severityTotals)
        {
            var month = totalChartDataMonth.Month;
            var envestOnlyChartData = envestChartData.Single(x => x.Month == month);
            var row = new LayeredBarChartData(totalChartDataMonth, envestOnlyChartData);
            chartData.Add(row);
        }

        exporter.SetFileNameMode(FileNameMode.ExactName, $"{Key}-SeveritiesEnvest");

        var fileName = exporter.Export(chartData, () => "Month,Totals,,,Envest Only\n,P1,P2,Other,EP1,EP2,EOther", SerialiseToCsv);
        sheetUpdater.CsvFilePathAndName = fileName;
        await sheetUpdater.EditSheet("'Envest'!A1");
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

        exporter.SetFileNameMode(FileNameMode.ExactName, $"{Key}-RecentDev");
        var fileName = exporter.Export(bugCounts, overrideSerialiseRecord: SerialiseToCsv);
        sheetUpdater.CsvFilePathAndName = fileName;
        await sheetUpdater.EditSheet("'RecentDev'!A1");
    }

    private async Task<List<BarChartData>> ExportBugStatsSeverities(List<JiraIssue> jiras, string? customerFilter = null)
    {
        var currentMonth = CalculateStartDate();
        var bugCounts = new List<BarChartData>();
        do
        {
            var filteredList = customerFilter is null
                ? jiras.Where(i => i.Created >= currentMonth && i.Created < currentMonth.AddMonths(1)).ToList()
                : jiras.Where(i => i.Created >= currentMonth && i.Created < currentMonth.AddMonths(1) && i.Customer.Contains(customerFilter)).ToList();
            // ReSharper disable InconsistentNaming
            var p1sTotal = filteredList.Count(i => i.Severity == Constants.SeverityCritical);
            var p2sTotal = filteredList.Count(i => i.Severity == Constants.SeverityMajor);
            // ReSharper restore InconsistentNaming
            var othersTotal = filteredList.Count - p1sTotal - p2sTotal;
            bugCounts.Add(new BarChartData(currentMonth, p1sTotal, p2sTotal, othersTotal));
            currentMonth = currentMonth.AddMonths(1);
        } while (currentMonth < DateTime.Today);

        if (customerFilter is null)
        {
            // Only update the master Severities total sheet if we're running without a specific customer filter.
            exporter.SetFileNameMode(FileNameMode.ExactName, $"{Key}-Severities");
            var fileName = exporter.Export(bugCounts, overrideSerialiseRecord: SerialiseToCsv);
            sheetUpdater.CsvFilePathAndName = fileName;
            await sheetUpdater.EditSheet("'Severities'!A1");
        }

        return bugCounts;
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
            return codeArea;
        }

        var myCodeArea = string.IsNullOrWhiteSpace(codeArea) ? Constants.Unknown : codeArea.Trim();
        var myCodeAreaParent = string.IsNullOrWhiteSpace(codeAreaParent) ? Constants.Unknown : codeAreaParent.Trim();

        return $"{myCodeArea} / {myCodeAreaParent}";
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

        if (record is LayeredBarChartData layeredBarChartData)
        {
            var builder = new StringBuilder();
            builder.Append(layeredBarChartData.Total.Month.ToString("MMM-yy"));
            builder.Append($",{layeredBarChartData.Total.P1s}");
            builder.Append($",{layeredBarChartData.Total.P2s}");
            builder.Append($",{layeredBarChartData.Total.Others}");
            builder.Append($",{layeredBarChartData.Envest.P1s}");
            builder.Append($",{layeredBarChartData.Envest.P2s}");
            builder.Append($",{layeredBarChartData.Envest.Others}");
            return builder.ToString();
        }

        throw new InvalidOperationException("Unsupported record type for CSV serialization.");
    }

    // ReSharper disable InconsistentNaming
    private record BarChartData(DateTime Month, int P1s, int P2s, int Others);
    // ReSharper restore InconsistentNaming

    private record LayeredBarChartData(BarChartData Total, BarChartData Envest);

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
        string? CodeArea,
        string Customer);
}
