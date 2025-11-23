namespace BensJiraConsole.Tasks;

public class OpenIncidentDashboard(IJiraQueryRunner runner, IWorkSheetUpdater sheetUpdater) : IJiraExportTask
{
    private const string TaskKey = "INCIDENTS";
    private const string GoogleSheetId = "1M5ftE2dtQ1l-NoSL6sqkLynVOpHFeixVBF0faOzefvw";
    private const string GoogleSheetTabName = "Open Incident Dashboard";
    private const string NoSprintAssigned = "No Sprint";

    private static readonly IFieldMapping[] Fields =
    [
        JiraFields.Status,
        JiraFields.Summary,
        JiraFields.Team,
        JiraFields.StoryPoints,
        JiraFields.Sprint,
        JiraFields.Severity
    ];

    private readonly List<IList<object?>> sheetData = new();

    public string Description => "Pulls data from Jira and Slack to give a combined view of all open incidents.";
    public string Key => TaskKey;

    public async Task ExecuteAsync(string[] args)
    {
        SetLastUpdateTime();
        var jiraIssues = await RetrieveJiraData(Constants.JavPmJiraProjectKey);
        CreateTableForOpenTicketSummary(jiraIssues);
        CreateTableForTeamVelocity(jiraIssues);

        await sheetUpdater.Open(GoogleSheetId);
        sheetUpdater.EditSheet($"{GoogleSheetTabName}!A1", this.sheetData, true);
        await sheetUpdater.SubmitBatch();
    }

    private void CreateTableForOpenTicketSummary(IReadOnlyList<JiraIssue> jiraIssues)
    {
        this.sheetData.Add([null, "Number if P1s", "Number of P2s"]);
        this.sheetData.Add([
            "Open Tickets:",
            jiraIssues.Count(i => i.Severity == Constants.SeverityCritical),
            jiraIssues.Count(i => i.Severity == Constants.SeverityMajor)
        ]);
        this.sheetData.Add([
            "In Sprint:",
            jiraIssues.Count(i => i.Severity == Constants.SeverityCritical && !string.IsNullOrWhiteSpace(i.Sprint) && i.Sprint != NoSprintAssigned),
            jiraIssues.Count(i => i.Severity == Constants.SeverityMajor && !string.IsNullOrWhiteSpace(i.Sprint) && i.Sprint != NoSprintAssigned)
        ]);

        // Group by customer
        var issuesByCustomer = jiraIssues.GroupBy(i => i.Customer);
        var rank = 1;
        foreach (var group in issuesByCustomer)
        {
            this.sheetData.Add([
                $"{rank}) {group.Key}",
                group.Count(i => i.Severity == Constants.SeverityCritical),
                group.Count(i => i.Severity == Constants.SeverityMajor)
            ]);
        }

        this.sheetData.Add([]);
        this.sheetData.Add([]);
    }

    private void CreateTableForTeamVelocity(IReadOnlyList<JiraIssue> jiraIssues)
    {
        this.sheetData.Add(["Team Velocity (Last 5 sprints)", "P1s", "P2s", "Other"]);
        this.sheetData.Add(["Total"]);

        // TODO
        // How to find a list of sprint numbers for a team?
    }

    private async Task<IReadOnlyList<JiraIssue>> RetrieveJiraData(string project)
    {
        var jql = $"project = {project} AND issueType = Bug AND status != Done";
        var issues = (await runner.SearchJiraIssuesWithJqlAsync(jql, Fields)).Select(JiraIssue.CreateJiraIssue);
        // TODO Likely need to calc last days activity here
        return issues.ToList();
    }

    private void SetLastUpdateTime()
    {
        var row = new List<object?> { null, DateTime.Now.ToString("d-MMM-yy HH:mm") };
        this.sheetData.Add(row);
        this.sheetData.Add(new List<object?>());
    }

    private record JiraIssue(
        string Key,
        string Summary,
        string Sprint,
        string Customer,
        string Severity,
        string Team,
        string Status,
        int LastActivity)
    {
        public static JiraIssue CreateJiraIssue(dynamic d)
        {
            return new JiraIssue(
                Key: JiraFields.Key.Parse(d),
                Summary: JiraFields.Summary.Parse(d),
                Sprint: JiraFields.Sprint.Parse(d) ?? NoSprintAssigned,
                Customer: JiraFields.CustomersMultiSelect.Parse(d) ?? string.Empty,
                Severity: JiraFields.Severity.Parse(d) ?? string.Empty,
                Team: JiraFields.Team.Parse(d) ?? "No Team",
                Status: JiraFields.Status.Parse(d),
                LastActivity: 999 //TODO
            );
        }
    }
}
