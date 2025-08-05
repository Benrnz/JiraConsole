using System.Net;

namespace BensJiraConsole.Tasks;

// ReSharper disable once UnusedType.Global
public class ExportPmPlanBurnUpData : IJiraExportTask
{
    public string Key => "PMPLAN_STORIES";
    public string Description => "Export PM Plan data for drawing a release burn-up chart";

    public FieldMapping[] Fields =>
    [
        //  JIRA Field Name,          Friendly Alias,                    Flatten object field name
        new("summary", "Summary"),
        new("status", "Status", "name"),
        new("parent", "Parent", "key"),
        new("customfield_10004", "StoryPoints"),
        new("timeoriginalestimate", "Original Estimate"),
        new("created"),
        new("resolutiondate", "Resolved"),
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

    public async Task ExecuteAsync(string[] fields)
    {
        Console.WriteLine(Description);
        var jqlPmPlans = "IssueType = Idea AND \"PM Customer[Checkboxes]\"= Envest AND \"Required for Go-live[Checkbox]\" = 1 AND \"Estimation Status[Dropdown]\" = \"Dev Team Estimate\" ORDER BY Key";
        Console.WriteLine(jqlPmPlans);
        var childrenJql = "project=JAVPM AND (issue in (linkedIssues(\"{0}\")) OR parent in (linkedIssues(\"{0}\"))) ORDER BY key";
        Console.WriteLine($"ForEach PMPLAN: {childrenJql}");
        var dynamicRunner = new JiraQueryDynamicRunner();
        var pmPlans = await dynamicRunner.SearchJiraIssuesWithJqlAsync(jqlPmPlans, PmPlanFields);

        var allIssues = new List<JiraIssue>();
        foreach (var pmPlan in pmPlans)
        {
            List<dynamic> children = await dynamicRunner.SearchJiraIssuesWithJqlAsync(string.Format(childrenJql, pmPlan.key), Fields);
            Console.WriteLine($"Fetched {children.Count} children for {pmPlan.key}");
            var range = children.Select(c => new JiraIssue(
                (string)c.key,
                (DateTime)c.created.UtcDateTime,
                (DateTime?)c.Resolved?.UtcDateTime,
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

        ExportToCsv(charts);
    }

    private void ExportToCsv(IDictionary<string, IEnumerable<BurnUpChartData>> charts)
    {
        foreach (var pmPlan in charts.Keys)
        {
            var exporter = new SimpleCsvExporter { Mode = SimpleCsvExporter.FileNameMode.ExactName };
            var fileName = exporter.Export(charts[pmPlan], pmPlan);
            Console.WriteLine(Path.GetFullPath(fileName));
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
            var date = children.Min(i => i.CreatedDateTime);
            if (date == DateTime.MinValue)
            {
                continue;
            }

            while (date <= DateTime.Today)
            {
                var dataPoint = new BurnUpChartData
                {
                    Date = date,
                    TotalDaysEffort = rawData
                        .Where(i => i.CreatedDateTime <= date).Sum(i => i.StoryPoints),
                    WorkCompleted = rawData
                        .Where(i => i.ResolvedDateTime <= date && i.Status == "Done")
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

    private record JiraIssue(string Key, DateTime CreatedDateTime, DateTime? ResolvedDateTime, string Status, double? StoryPoints, string PmPlan);
}

public class BurnUpChartData
{
    public DateTime Date { get; set; }

    public double? TotalDaysEffort { get; set; }

    public double? WorkCompleted { get; set; }
}