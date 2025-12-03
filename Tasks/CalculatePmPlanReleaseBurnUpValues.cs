namespace BensEngineeringMetrics.Tasks;

// ReSharper disable once UnusedType.Global
public class CalculatePmPlanReleaseBurnUpValues(ICsvExporter exporter, ExportPmPlanStories pmPlanStoriesTask) : IEngineeringMetricsTask
{
    private const string KeyString = "PMPLAN_RBURNUP";

    public string Key => KeyString;
    public string Description => "Calculate Overall _PMPlan_Release_Burn_Up_";

    public async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine($"{Key} - {Description}");

        var javPms = (await pmPlanStoriesTask.RetrieveAllStoriesMappingToPmPlan()).ToList();
        exporter.SetFileNameMode(FileNameMode.Hint, Key);
        exporter.Export(javPms);

        var totalWork = CalculateTotalWorkToBeDone(javPms, pmPlanStoriesTask.PmPlans);
        var workCompleted = CalculateCompletedWork(javPms);
        var highLevelEstimates = pmPlanStoriesTask.PmPlans.Count(p => p.IsReqdForGoLive > 0.01 && p.EstimationStatus != Constants.HasDevTeamEstimate && p.PmPlanHighLevelEstimate > 0);
        var noEstimates = pmPlanStoriesTask.PmPlans.Count(p =>
            p.IsReqdForGoLive > 0.01 && p.EstimationStatus != Constants.HasDevTeamEstimate && (p.PmPlanHighLevelEstimate is null || p.PmPlanHighLevelEstimate == 0));
        var specedAndEstimated = pmPlanStoriesTask.PmPlans.Count(p => p.IsReqdForGoLive > 0.01 && p.EstimationStatus == Constants.HasDevTeamEstimate);
        var storiesWithNoEstimate = javPms.Count(i => i.IsReqdForGoLive && i.Status != Constants.DoneStatus && i.StoryPoints == 0);
        var avgVelocity = javPms.Where(i => i.Status == Constants.DoneStatus && i.CreatedDateTime >= DateTimeOffset.Now.AddDays(-42))
                              .Sum(i => i.StoryPoints)
                          / 3.0; // 6 weeks or 3 sprints.

        Console.WriteLine($"As at {DateTime.Today:d}");
        Console.WriteLine($"Total work to be done: {totalWork}");
        Console.WriteLine($"Work completed: {workCompleted}");
        Console.WriteLine($"PmPlans with High level estimates only: {highLevelEstimates}");
        Console.WriteLine($"PmPlans with no estimate: {noEstimates}");
        Console.WriteLine($"PmPlans with Spec'ed and Estimated: {specedAndEstimated}");
        Console.WriteLine($"Stories with no estimate: {storiesWithNoEstimate} / {javPms.Count(i => i.IsReqdForGoLive && i.Status != Constants.DoneStatus)}");
        Console.WriteLine($"Average Velocity (last 6 weeks): {avgVelocity:N1} story points per sprint");
    }

    private double CalculateCompletedWork(List<ExportPmPlanStories.JiraIssueWithPmPlan> jiraIssues)
    {
        return jiraIssues
            .Where(issue => issue.IsReqdForGoLive && issue is { EstimationStatus: Constants.HasDevTeamEstimate, Status: Constants.DoneStatus })
            .Sum(issue => issue.StoryPoints);
    }

    private double CalculateTotalWorkToBeDone(List<ExportPmPlanStories.JiraIssueWithPmPlan> jiraIssues, IEnumerable<dynamic> pmPlans)
    {
        var myList = jiraIssues.ToList();
        var totalWork = myList
            .Where(issue => issue is { IsReqdForGoLive: true, EstimationStatus: Constants.HasDevTeamEstimate })
            .Sum(issue => issue.StoryPoints);

        totalWork += pmPlans.Where(p => (double?)p.IsReqdForGoLive > 0.01 && p.EstimationStatus != Constants.HasDevTeamEstimate).Sum(p => (double?)p.PmPlanHighLevelEstimate ?? 0.0);

        return totalWork;
    }
}
