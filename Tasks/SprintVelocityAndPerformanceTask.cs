namespace BensJiraConsole.Tasks;

public class SprintVelocityAndPerformanceTask : IJiraExportTask
{
    private const string GoogleSheetId = "";
    private const string TaskKey = "SPRINT_PERF";

    private static readonly IFieldMapping[] Fields =
    [
        JiraFields.Status,
        JiraFields.Team,
        JiraFields.StoryPoints,
        JiraFields.Sprint,
        JiraFields.SprintStartDate,
        JiraFields.IssueType
    ];

    public string Description => "Export to a Google Sheet the last 12 months of sprint velocity and performance metrics.";

    public string Key => TaskKey;

    public Task ExecuteAsync(string[] args)
    {
        // https://javlnsupport.atlassian.net/rest/greenhopper/1.0/rapid/charts/sprintreport?rapidViewId=419&sprintId=2063
        // rapidViewId is boardID. Superclass for example is board id 419


    }
}
