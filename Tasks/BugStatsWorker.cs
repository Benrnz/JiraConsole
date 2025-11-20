using System.Text;

namespace BensJiraConsole.Tasks;

public class BugStatsWorker(IJiraQueryRunner runner, ICsvExporter exporter, IWorkSheetUpdater sheetUpdater)
{
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
        JiraFields.CustomersMultiSelect
    ];

    private List<string> allCategories = new();

    private List<string> allCodeAreas = new();
    private string keyString = string.Empty;

    public void Clear()
    {
        this.allCategories.Clear();
        this.allCodeAreas.Clear();
    }

    public async Task UpdateSheet(string jiraProjectKey, string googleSheetId)
    {
        try
        {
            this.keyString = jiraProjectKey;
            var jql = $"""project = {jiraProjectKey} AND issuetype = Bug AND "Bug Type[Dropdown]" IN (Production, UAT) AND created >= startOfMonth("-13M")""";
            Console.WriteLine(jql);
            Console.WriteLine();
            var jiras = (await runner.SearchJiraIssuesWithJqlAsync(jql, Fields))
                .Select(CreateJiraIssue)
                .OrderBy(i => i.Created)
                .ToList();

            await sheetUpdater.Open(googleSheetId);
            await ExportBugStatsCodeAreas(jiras);
            if (await sheetUpdater.DoesSheetExist(googleSheetId, "CodeAreasExclEnvest"))
            {
                await ExportBugStatsCodeAreasExclEnvest(jiras);
            }

            await ExportBugStatsRecentDevelopment(jiras);
            if (await sheetUpdater.DoesSheetExist(googleSheetId, "RecentDevExclEnvest"))
            {
                await ExportBugStatsRecentDevelopmentExclEnvest(jiras);
            }

            var monthTotalsForSeverities = await ExportBugStatsSeverities(jiras);
            await ExportBugStatsReportedVsBacklog(monthTotalsForSeverities);
            await ExportBugStatsReportedVsResolved(monthTotalsForSeverities);
            await ExportBugStatsEnvestSeverities(jiras, monthTotalsForSeverities);
            await ExportBugStatsCategories(jiras);
            sheetUpdater.EditSheet("Info!B1", [[DateTime.Now.ToString("g")]]);
        }
        finally
        {
            await sheetUpdater.SubmitBatch();
        }
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

        exporter.SetFileNameMode(FileNameMode.ExactName, $"{this.keyString}-Categories");
        var fileName = exporter.Export(bugCounts, SerialiseCatergoriesHeaderRow, SerialiseToCsv);

        await sheetUpdater.ImportFile("'ProductCategories'!A1", fileName);
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

        exporter.SetFileNameMode(FileNameMode.ExactName, $"{this.keyString}-Areas");
        var fileName = exporter.Export(bugCounts, SerialiseCodeAreasHeaderRow, SerialiseToCsv);

        await sheetUpdater.ImportFile("'CodeAreas'!A1", fileName);
    }

    private async Task ExportBugStatsCodeAreasExclEnvest(List<JiraIssue> jiras)
    {
        var currentMonth = CalculateStartDate();
        var bugCounts = new List<BarChartDataCodeAreas>();
        this.allCodeAreas = ParseCodeAreas(jiras.Select(j => JoinCodeAreasParent(j.CodeArea, j.CodeAreaParent)).Distinct().ToList());
        do
        {
            var filteredList = jiras.Where(i => !i.Customer.Contains(Constants.Envest) && i.Created >= currentMonth && i.Created < currentMonth.AddMonths(1)).ToList();
            var areaCounts = new Dictionary<string, int>();
            foreach (var codeArea in this.allCodeAreas)
            {
                areaCounts[codeArea] = filteredList.Count(j => CodeAreaMatches(j.CodeArea, j.CodeAreaParent, codeArea));
            }

            bugCounts.Add(new BarChartDataCodeAreas(currentMonth, areaCounts));
            currentMonth = currentMonth.AddMonths(1);
        } while (currentMonth < DateTime.Today);

        exporter.SetFileNameMode(FileNameMode.ExactName, $"{this.keyString}-AreasExclEnvest");
        var fileName = exporter.Export(bugCounts, SerialiseCodeAreasHeaderRow, SerialiseToCsv);

        await sheetUpdater.ImportFile("'CodeAreasExclEnvest'!A1", fileName);
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

        exporter.SetFileNameMode(FileNameMode.ExactName, $"{this.keyString}-SeveritiesEnvest");

        var fileName = exporter.Export(chartData, () => "Month,Totals,,,Envest Only\n,P1,P2,Other,EP1,EP2,EOther", SerialiseToCsv);
        await sheetUpdater.ImportFile("'Envest'!A1", fileName);
    }

    private async Task ExportBugStatsRecentDevelopment(List<JiraIssue> jiras)
    {
        var currentMonth = CalculateStartDate();
        var bugCounts = new List<BarChartData>();
        do
        {
            var filteredList = jiras
                .Where(i => i.Created >= currentMonth && i.Created < currentMonth.AddMonths(1) && i is { Resolution: Constants.CodeFixLast6, BugType: Constants.BugTypeProduction })
                .ToList();
            // ReSharper disable InconsistentNaming
            var p1sTotal = filteredList.Count(i => i.Severity == Constants.SeverityCritical);
            var p2sTotal = filteredList.Count(i => i.Severity == Constants.SeverityMajor);
            // ReSharper restore InconsistentNaming
            var othersTotal = filteredList.Count - p1sTotal - p2sTotal;
            bugCounts.Add(new BarChartData(currentMonth, p1sTotal, p2sTotal, othersTotal));
            currentMonth = currentMonth.AddMonths(1);
        } while (currentMonth < DateTime.Today);

        exporter.SetFileNameMode(FileNameMode.ExactName, $"{this.keyString}-RecentDev");
        var fileName = exporter.Export(bugCounts, overrideSerialiseRecord: SerialiseToCsv);
        await sheetUpdater.ImportFile("'RecentDev'!A1", fileName);
    }

    private async Task ExportBugStatsRecentDevelopmentExclEnvest(List<JiraIssue> jiras)
    {
        var currentMonth = CalculateStartDate();
        var bugCounts = new List<BarChartData>();
        do
        {
            var filteredList = jiras
                .Where(i =>
                    !i.Customer.Contains(Constants.Envest)
                    && i.Created >= currentMonth
                    && i.Created < currentMonth.AddMonths(1)
                    && i is { Resolution: Constants.CodeFixLast6, BugType: Constants.BugTypeProduction })
                .ToList();
            // ReSharper disable InconsistentNaming
            var p1sTotal = filteredList.Count(i => i.Severity == Constants.SeverityCritical);
            var p2sTotal = filteredList.Count(i => i.Severity == Constants.SeverityMajor);
            // ReSharper restore InconsistentNaming
            var othersTotal = filteredList.Count - p1sTotal - p2sTotal;
            bugCounts.Add(new BarChartData(currentMonth, p1sTotal, p2sTotal, othersTotal));
            currentMonth = currentMonth.AddMonths(1);
        } while (currentMonth < DateTime.Today);

        exporter.SetFileNameMode(FileNameMode.ExactName, $"{this.keyString}-RecentDevExclEnvest");
        var fileName = exporter.Export(bugCounts, overrideSerialiseRecord: SerialiseToCsv);
        await sheetUpdater.ImportFile("'RecentDevExclEnvest'!A1", fileName);
    }

    private async Task ExportBugStatsReportedVsBacklog(List<BarChartData> severityTotals)
    {
        var chartData = new List<LayeredBarChartData>();
        var jql = $"""project = {this.keyString} AND issuetype = Bug AND "Bug Type[Dropdown]" IN (Production, UAT) AND status != Done""";
        var issuesBacklog = (await runner.SearchJiraIssuesWithJqlAsync(jql, Fields)).Select(CreateJiraIssue).ToList();
        foreach (var totalChartDataMonth in severityTotals)
        {
            var month = totalChartDataMonth.Month;
            var monthIssueBacklog = issuesBacklog.Where(x => x.Created < month.AddMonths(1)).ToList();
            var p1Backlog = monthIssueBacklog.Count(x => x.Severity == Constants.SeverityCritical);
            var p2Backlog = monthIssueBacklog.Count(x => x.Severity == Constants.SeverityMajor);
            var otherBacklog = monthIssueBacklog.Count() - p1Backlog - p2Backlog;
            var monthbacklogData = new BarChartData(month, p1Backlog, p2Backlog, otherBacklog);
            var row = new LayeredBarChartData(totalChartDataMonth, monthbacklogData);
            chartData.Add(row);
        }

        exporter.SetFileNameMode(FileNameMode.ExactName, $"{this.keyString}-ReportVsBacklog");

        var fileName = exporter.Export(chartData, () => "Month,New Bugs Reported (Left Axis),,,Ticket Backlog (Right Axis)\n,P1,P2,Other,Open P1s,Open P2s,Open Others", SerialiseToCsv);
        await sheetUpdater.ImportFile("'Reported Vs Backlog'!A1", fileName);
    }

    private async Task ExportBugStatsReportedVsResolved(List<BarChartData> severityTotals)
    {
        var chartData = new List<LayeredBarChartData>();
        var jql =
            $"""project = "{this.keyString}" AND status = Done AND type = Bug AND Severity IN (Critical, Major) AND "Bug Type[Dropdown]" IN (Production, UAT) AND resolutiondate >= startOfMonth("-13M") ORDER BY resolutiondate""";
        var issuesBacklog = (await runner.SearchJiraIssuesWithJqlAsync(jql, Fields)).Select(CreateJiraIssue).ToList();
        foreach (var totalChartDataMonth in severityTotals)
        {
            var month = totalChartDataMonth.Month;
            var monthIssueBacklog = issuesBacklog.Where(x => x.Resolved >= month && x.Resolved < month.AddMonths(1)).ToList();
            var p1Resolved = monthIssueBacklog.Count(x => x.Severity == Constants.SeverityCritical);
            var p2Resolved = monthIssueBacklog.Count(x => x.Severity == Constants.SeverityMajor);
            var otherResolved = monthIssueBacklog.Count() - p1Resolved - p2Resolved;
            var monthbacklogData = new BarChartData(month, p1Resolved, p2Resolved, otherResolved);
            var row = new LayeredBarChartData(totalChartDataMonth, monthbacklogData);
            chartData.Add(row);
        }

        exporter.SetFileNameMode(FileNameMode.ExactName, $"{this.keyString}-ReportVsResolved");

        var fileName = exporter.Export(chartData, () => "Month,New Bugs Reported (Left Axis),,,Resolved Bugs (Right Axis)\n,P1,P2,Other,Resolved P1s,Resolved P2s,Resolved Others", SerialiseToCsv);
        await sheetUpdater.ImportFile("'Reported Vs Resolved'!A1", fileName);
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
            exporter.SetFileNameMode(FileNameMode.ExactName, $"{this.keyString}-Severities");
            var fileName = exporter.Export(bugCounts, overrideSerialiseRecord: SerialiseToCsv);
            await sheetUpdater.ImportFile("'Severities'!A1", fileName);
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
            builder.Append(layeredBarChartData.DataSet1.Month.ToString("MMM-yy"));
            builder.Append($",{layeredBarChartData.DataSet1.P1s}");
            builder.Append($",{layeredBarChartData.DataSet1.P2s}");
            builder.Append($",{layeredBarChartData.DataSet1.Others}");
            builder.Append($",{layeredBarChartData.DataSet2.P1s}");
            builder.Append($",{layeredBarChartData.DataSet2.P2s}");
            builder.Append($",{layeredBarChartData.DataSet2.Others}");
            return builder.ToString();
        }

        throw new InvalidOperationException("Unsupported record type for CSV serialization.");
    }

    // ReSharper disable InconsistentNaming
    private record BarChartData(DateTime Month, int P1s, int P2s, int Others);
    // ReSharper restore InconsistentNaming

    private record LayeredBarChartData(BarChartData DataSet1, BarChartData DataSet2);

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
