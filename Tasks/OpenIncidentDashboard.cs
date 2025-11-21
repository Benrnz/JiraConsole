namespace BensJiraConsole.Tasks;

public class OpenIncidentDashboard : IJiraExportTask
{
    private const string TaskKey = "INCIDENTS";
    private const string GoogleSheetId = "";

    private static readonly IFieldMapping[] Fields =
    [
        JiraFields.Status,
        JiraFields.Summary,
        JiraFields.Team,
        JiraFields.StoryPoints,
        JiraFields.Sprint,
        JiraFields.SprintStartDate,
        JiraFields.IssueType
    ];

    public string Description => "Pulls data from Jira and Slack to give a combined view of all open incidents.";
    public string Key => TaskKey;
    public Task ExecuteAsync(string[] args)
    {
        throw new NotImplementedException();
    }

    private record JiraIssue(string Key, string Summary, string Sprint, string Customer, string Severity, string Team, string LastActivity);
}
