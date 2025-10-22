using System.Management;

namespace BensJiraConsole.Tasks;

public class SprintPlanTask(IJiraQueryRunner runner, ICsvExporter exporter, IWorkSheetUpdater sheetUpdater, ExportPmPlanStories pmPlanStoriesTask) : IJiraExportTask
{
    private const string GoogleSheetId = "1iS6iB3EA38SHJgDu8rpMFcouGlu1Az8cntKA52U07xU";
    private const string TaskKey = "SPRINT_PLAN";

    private static readonly IFieldMapping[] Fields =
    [
        JiraFields.Summary,
        JiraFields.Status,
        JiraFields.Team,
        JiraFields.StoryPoints,
        JiraFields.Priority,
        JiraFields.Team,
        JiraFields.StoryPoints,
        JiraFields.Sprint,
        JiraFields.SprintStartDate,
        JiraFields.IssueType,
        JiraFields.ParentKey
    ];

    public string Description => "Export to a Google Sheet a over-arching plan of all future sprints for the two project teams.";
    public string Key => TaskKey;

    public async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine(Description);
        var query = """project = "JAVPM" AND "Team[Team]" IN (60412efa-7e2e-4285-bb4e-f329c3b6d417, 1a05d236-1562-4e58-ae88-1ffc6c5edb32) AND (Sprint IN openSprints() OR Sprint IN futureSprints())""";
        Console.WriteLine(query);
        Console.WriteLine();

        // Get and group the data by Team and by Sprint.
        var result = await runner.SearchJiraIssuesWithJqlAsync(query, Fields);
        var issues = result.Select(CreateJiraIssue).ToList();

        // Find PMPLAN for each issue if it exists.
        var pmPlanStories = await pmPlanStoriesTask.RetrieveAllStoriesMappingToPmPlan();
        issues.Join(pmPlanStories, i => i.Key, p => p.Key, (i, p) => (Issue: i, PmPlan: p))
            .ToList()
            .ForEach(x =>
            {
                x.Issue.PmPlan = x.PmPlan.PmPlan;
                x.Issue.PmPlanSummary = x.PmPlan.PmPlanSummary;
            });

        issues = issues.OrderBy(i => i.Team)
            .ThenBy(i => i.SprintStartDate)
            .ThenBy(i => i.Sprint)
            .ThenBy(i => i.PmPlan)
            .ToList();

        // temp save to CSV
        exporter.SetFileNameMode(FileNameMode.Auto, Key + "_FullData");
        var file = exporter.Export(issues);

        // Export to Google Sheets.
        await sheetUpdater.Open(GoogleSheetId);
        sheetUpdater.CsvFilePathAndName = file;
        await sheetUpdater.ClearSheet("Data");
        await sheetUpdater.ImportFile("'Data'!A1");

        await SprintPmPlan(issues);
    }

    private async Task SprintPmPlan(List<JiraIssue> issues)
    {
        var groupBySprint = issues
            .GroupBy(i => (i.Team, i.SprintStartDate, i.Sprint, i.PmPlan, i.PmPlanSummary))
            .OrderBy(g => g.Key.Team)
            .ThenBy(g => g.Key.SprintStartDate)
            .ThenBy(g => g.Key.Sprint)
            .Select(g => new
            {
                Team = g.Key.Team,
                StartDate = g.Key.SprintStartDate,
                SprintName = g.Key.Sprint,
                PMPLAN = g.Key.PmPlan,
                Summary = g.Key.PmPlanSummary,
                StoryPoints = g.Sum(x => x.StoryPoints),
                Tickets = g.Count()
            })
            .ToList();

        var sheetData = new List<IList<object?>> { new List<object?> { "Team", "Sprint Name", "Start Date", "PMPLAN", "Summary", "Tickets", "Story Points" } };
        var teamSprint = string.Empty;
        foreach (var row in groupBySprint.OrderBy(g => g.StartDate).ThenBy(g => g.Team).ThenBy(g => g.SprintName))
        {
            if (row.Team + row.SprintName != teamSprint)
            {
                var rowData1 = new List<object?>
                {
                    row.Team,
                    row.SprintName,
                    row.StartDate == DateTimeOffset.MaxValue ? null : row.StartDate.ToString("d-MMM-yy"),
                    null,
                    null,
                    groupBySprint.Where(g => g.StartDate == row.StartDate && g.SprintName == row.SprintName && g.Team == row.Team).Sum(g => g.Tickets),
                    groupBySprint.Where(g => g.StartDate == row.StartDate && g.SprintName == row.SprintName && g.Team == row.Team).Sum(g => g.StoryPoints),
                };
                sheetData.Add(rowData1);
            }

            var rowData = new List<object?>
            {
                null,
                null,
                null,
                string.IsNullOrEmpty(row.PMPLAN) ? "No PMPLAN" : row.PMPLAN,
                row.Summary,
                row.Tickets,
                row.StoryPoints
            };
            sheetData.Add(rowData);
            teamSprint = row.Team + row.SprintName;
        }

        await sheetUpdater.Open(GoogleSheetId);
        await sheetUpdater.ClearSheet("Sprints-PMPlans");
        await sheetUpdater.EditSheet("Sprints-PMPlans!A1", sheetData);
        //await sheetUpdater.ApplyDateFormat("Sprints-PMPlans", 2, "d mmm yy");
    }

    private JiraIssue CreateJiraIssue(dynamic i)
    {
        string key = JiraFields.Key.Parse(i);
        string sprintField = JiraFields.Sprint.Parse(i) ?? "No Sprint";
        var sprintDate = JiraFields.SprintStartDate.Parse(i);
        var teamField = JiraFields.Team.Parse(i) ?? "No Team";
        var storyPointsField = JiraFields.StoryPoints.Parse(i) ?? 0.0;

        var typedIssue = new JiraIssue(
            teamField,
            sprintField,
            sprintDate,
            key,
            JiraFields.Summary.Parse(i),
            storyPointsField,
            JiraFields.Status.Parse(i),
            JiraFields.IssueType.Parse(i),
            JiraFields.ParentKey.Parse(i));
        return typedIssue;
    }

    private record JiraIssue(
        string Team,
        string Sprint,
        DateTimeOffset SprintStartDate,
        string Key,
        string Summary,
        double StoryPoints,
        string Status,
        string Type,
        string? ParentEpic = null)
    {
        public string? PmPlan { get; set; }
        public string? PmPlanSummary { get; set; }
    }
}
