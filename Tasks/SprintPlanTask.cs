namespace BensJiraConsole.Tasks;

public class SprintPlanTask : IJiraExportTask
{
    private const string GoogleSheetId = "1iS6iB3EA38SHJgDu8rpMFcouGlu1Az8cntKA52U07xU";
    private const string TaskKey = "SPRINT_PLAN";

    private static readonly FieldMapping[] Fields =
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

    private readonly ICsvExporter exporter = new SimpleCsvExporter(TaskKey);

    private readonly IJiraQueryRunner runner = new JiraQueryDynamicRunner();

    private readonly IWorkSheetUpdater sheetUpdater = new GoogleSheetUpdater(GoogleSheetId);

    public string Description => "Export to a Google Sheet a over-arching plan of all future sprints for the two project teams.";
    public string Key => TaskKey;

    public async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine(Description);
        var query = """project = "JAVPM" AND "Team[Team]" IN (60412efa-7e2e-4285-bb4e-f329c3b6d417, 1a05d236-1562-4e58-ae88-1ffc6c5edb32) AND (Sprint IN openSprints() OR Sprint IN futureSprints())""";
        Console.WriteLine(query);
        Console.WriteLine();

        // Get and group the data by Team and by Sprint.
        var result = await this.runner.SearchJiraIssuesWithJqlAsync(query, Fields);
        var issues = result.Select(CreateJiraIssue)
            .OrderBy(i => i.Team)
            .ThenBy(i => i.SprintStartDate)
            .ThenBy(i => i.Sprint)
            .ToList();

        // Find PMPLAN for each issue if it exists.
        var pmPlanStories = await new ExportPmPlanStories().RetrieveAllStoriesMappingToPmPlan();
        issues.Join(pmPlanStories, i => i.Key, p => p.Key, (i, p) => (Issue: i, PmPlan: p))
            .ToList()
            .ForEach(x =>
            {
                x.Issue.PmPlan = x.PmPlan.PmPlan;
            });

        // temp save to CSV
        var file = this.exporter.Export(issues);

        // Export to Google Sheets.
        this.sheetUpdater.CsvFilePathAndName = file;
        await this.sheetUpdater.DeleteSheet("Data");
        await this.sheetUpdater.AddSheet("Data");
        await this.sheetUpdater.EditGoogleSheet("'Data'!A1");
    }

    private JiraIssue CreateJiraIssue(dynamic i)
    {
        string? sprintField = JiraFields.Sprint.Parse<string?>(i) ?? "No Sprint";
        // Sprint could have multiple sprint names for example "Sprint 15,Sprint 16".  Choose the last one for this report.
        if (sprintField.Contains(','))
        {
            sprintField = sprintField.Split(',').Last();
        }
        // SprintStartDate could be multiple for example "2025-08-09T01:01:01.000+00:00,2025-08-23T01:01:01.000+00:00"
        string? sprintDates = JiraFields.SprintStartDate.Parse<string?>(i);
        var sprintDateParsed = DateTimeOffset.MaxValue; 
        if (sprintDates is not null)
        {
            var sprintDate = sprintDates.Split(',').LastOrDefault() ?? string.Empty;
            if (!DateTimeOffset.TryParse(sprintDate, out sprintDateParsed))
            {
                sprintDateParsed = DateTimeOffset.MaxValue;
            }
        }

        var teamField = JiraFields.Team.Parse<string?>(i) ?? "No Team";
        var storyPointsField = JiraFields.StoryPoints.Parse<double?>(i) ?? 0.0;

        var typedIssue = new JiraIssue(
            teamField,
            sprintField,
            sprintDateParsed,
            JiraFields.Key.Parse<string>(i),
            JiraFields.Summary.Parse<string>(i),
            storyPointsField,
            JiraFields.Status.Parse<string>(i),
            JiraFields.IssueType.Parse<string>(i),
            JiraFields.ParentKey.Parse<string?>(i));
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
    }
}
