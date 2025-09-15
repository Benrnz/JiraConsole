namespace BensJiraConsole.Tasks;

// ReSharper disable once UnusedType.Global
public class CalculatePmPlanReleaseBurnUpValues : IJiraExportTask
{
    public string Key => "PMPLAN_RBURNUP";
    public string Description => "Calculate Overall _PMPlan_Release_Burn_Up_";

    public async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine(Description);

        var task = new ExportPmPlanStories();
        var javPms = (await task.RetrieveAllStoriesMappingToPmPlan()).Values.Select(x => (JiraIssue)CreateJiraIssueFromDynamic(x, "PmPlanMapping")).ToList();
        var exporter = new SimpleCsvExporter(Key);
        exporter.Export(javPms);

        var totalWork = CalculateTotalWorkToBeDone(javPms, task.PmPlans);
        var workCompleted = CalculateCompletedWork(javPms);
        var highLevelEstimates = task.PmPlans.Count(p => p.IsReqdForGoLive > 0.01 && p.EstimationStatus != Constants.HasDevTeamEstimate && p.PmPlanHighLevelEstimate > 0);
        var noEstimates = task.PmPlans.Count(p => p.IsReqdForGoLive > 0.01 && p.EstimationStatus != Constants.HasDevTeamEstimate && (p.PmPlanHighLevelEstimate is null || p.PmPlanHighLevelEstimate == 0));
        var specedAndEstimated = task.PmPlans.Count(p => p.IsReqdForGoLive > 0.01 && p.EstimationStatus == Constants.HasDevTeamEstimate);
        var storiesWithNoEstimate = javPms.Count(i => i.IsReqdForGoLive && i.Status != Constants.DoneStatus && (i.StoryPoints is null || i.StoryPoints == 0));

        Console.WriteLine($"As at {DateTime.Today:d}");
        Console.WriteLine($"Total work to be done: {totalWork}");
        Console.WriteLine($"Work completed: {workCompleted}");
        Console.WriteLine($"PmPlans with High level estimates only: {highLevelEstimates}");
        Console.WriteLine($"PmPlans with no estimate: {noEstimates}");
        Console.WriteLine($"PmPlans with Spec'ed and Estimated: {specedAndEstimated}");
        Console.WriteLine($"Stories with no estimate: {storiesWithNoEstimate} / {javPms.Count(i => i.IsReqdForGoLive && i.Status != Constants.DoneStatus)}");
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
        var pmPlanKey = i.PmPlan as string;
        return new JiraIssue(
            JiraFields.Key.Parse<string>(i),
            JiraFields.Created.Parse<DateTimeOffset>(i),
            JiraFields.Status.Parse<string>(i),
            JiraFields.StoryPoints.Parse<double?>(i),
            source,
            JiraFields.IsReqdForGoLive.Parse<bool>(i),
            JiraFields.EstimationStatus.Parse<string>(i),
            JiraFields.PmPlanHighLevelEstimate.Parse<double?>(i),
            pmPlanKey);
    }

    private record JiraIssue(
        string Key,
        DateTimeOffset CreatedDateTime,
        string Status,
        double? StoryPoints,
        string Source,
        bool IsReqdForGoLive = false,
        string EstimationStatus = "",
        double? PmPlanHighLevelEstimate = null,
        string? PmPlan = null);
}
