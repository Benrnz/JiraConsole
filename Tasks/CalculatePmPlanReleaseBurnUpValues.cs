namespace BensJiraConsole.Tasks;

// ReSharper disable once UnusedType.Global
public class CalculatePmPlanReleaseBurnUpValues : IJiraExportTask
{
    public string Key => "PMPLAN_RBURNUP";
    public string Description => "Export Overall PM Plan Release Burn Up";

    public async Task ExecuteAsync(string[] fields)
    {
        Console.WriteLine(Description);

        var task = new ExportPmPlanMapping();
        var javPms = (await task.RetrieveAllStoriesMappingToPmPlan()).Values.Select(x => (JiraIssue)CreateJiraIssueFromDynamic(x, "PmPlanMapping")).ToList();

        var totalWork = CalculateTotalWorkToBeDone(javPms, task.PmPlans);
        var workCompleted = CalculateCompletedWork(javPms);

        Console.WriteLine($"As at ${DateTime.Today:d}");
        Console.WriteLine($"Total work to be done: {totalWork}");
        Console.WriteLine($"Work completed: {workCompleted}");
    }

    private double CalculateCompletedWork(List<JiraIssue> jiraIssues)
    {
        return jiraIssues
            .Where(issue => issue.IsReqdForGoLive && issue is { EstimationStatus: Constants.HasDevTeamEstimate, Status: Constants.DoneStatus })
            .Sum(issue => issue.StoryPoints ?? 0);
    }

    private double CalculateTotalWorkToBeDone(List<JiraIssue> jiraIssues, IEnumerable<dynamic> pmPlans)
    {
        var myList = jiraIssues.ToList();
        var totalWork = myList
            .Where(issue => issue is { IsReqdForGoLive: true, EstimationStatus: Constants.HasDevTeamEstimate })
            .Sum(issue => issue.StoryPoints ?? 0);

        totalWork += pmPlans.Where(p => (double?)p.IsReqdForGoLive > 0.01 && p.EstimationStatus != Constants.HasDevTeamEstimate).Sum(p => (double?)p.PmPlanHighLevelEstimate ?? 0.0);

        return totalWork;
    }

    private static JiraIssue CreateJiraIssueFromDynamic(dynamic i, string source)
    {
        var isReqdForGoLive = i.IsReqdForGoLive > 0.01;

        return new JiraIssue(
            (string)i.key,
            (DateTimeOffset)i.created,
            (string)i.Status,
            (double?)i.StoryPoints,
            source,
            isReqdForGoLive,
            (string)i.EstimationStatus,
            (double?)i.PmPlanHighLevelEstimate);
    }

    private record JiraIssue(string Key, DateTimeOffset CreatedDateTime, string Status, double? StoryPoints, string Source, bool IsReqdForGoLive = false,
        string EstimationStatus = "", double? PmPlanHighLevelEstimate = null, string? PmPlan = null);
}
