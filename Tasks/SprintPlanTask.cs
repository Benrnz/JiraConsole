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
        JiraFields.SprintStartDate
    ];

    private readonly ICsvExporter exporter = new SimpleCsvExporter(TaskKey);

    private readonly IJiraQueryRunner runner = new JiraQueryDynamicRunner();

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

        // temp save to CSV
        var file = this.exporter.Export(issues);

        // Export to Google Sheets.
    }

    private JiraIssue CreateJiraIssue(dynamic i)
    {
        var sprintField = JiraFields.Sprint.Parse<string?>(i) ?? "No Sprint";
        var sprintDate = JiraFields.SprintStartDate.Parse<string?>(i);
        if (!DateTimeOffset.TryParse(sprintDate, out DateTimeOffset sprintDateParsed))
        {
            sprintDateParsed = DateTimeOffset.MaxValue;
        }

        var teamField = JiraFields.Team.Parse<string?>(i) ?? "No Team";
        var storyPointsField = JiraFields.StoryPoints.Parse<double?>(i) ?? 0.0;

        var typedIssue = new JiraIssue(
            JiraFields.Key.Parse<string>(i),
            JiraFields.Summary.Parse<string>(i),
            teamField,
            sprintField,
            storyPointsField,
            JiraFields.Status.Parse<string>(i),
            sprintDateParsed);
        return typedIssue;
    }

    private record JiraIssue(
        string Key,
        string Summary,
        string Team,
        string Sprint,
        double StoryPoints,
        string Status,
        DateTimeOffset SprintStartDate);
}
