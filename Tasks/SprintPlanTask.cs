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
        exporter.SetFileNameMode(FileNameMode.Auto, Key);
        var file = exporter.Export(issues);

        // Export to Google Sheets.
        await sheetUpdater.Open(GoogleSheetId);
        sheetUpdater.CsvFilePathAndName = file;
        await sheetUpdater.ClearSheet("Data");
        await sheetUpdater.EditSheet("'Data'!A1");
    }

    private JiraIssue CreateJiraIssue(dynamic i)
    {
        string sprintField = JiraFields.Sprint.Parse(i) ?? "No Sprint";
        // Sprint could have multiple sprint names for example "Sprint 15,Sprint 16".  Choose the last one for this report.
        if (sprintField.Contains(','))
        {
            sprintField = sprintField.Split(',').Last();
        }

        var sprintDate = JiraFields.SprintStartDate.Parse(i);
        var teamField = JiraFields.Team.Parse(i) ?? "No Team";
        var storyPointsField = JiraFields.StoryPoints.Parse(i) ?? 0.0;

        var typedIssue = new JiraIssue(
            teamField,
            sprintField,
            sprintDate,
            JiraFields.Key.Parse(i),
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
