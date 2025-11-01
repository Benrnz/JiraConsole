namespace BensJiraConsole.Tasks;

public class SprintVelocityAndPerformanceTask(IGreenHopperClient greenHopperClient) : IJiraExportTask
{
    private const string GoogleSheetId = "";
    private const string TaskKey = "SPRINT_PERF";

    private readonly TeamSprint[] teams =
    [
        new("Superclass", Constants.TeamSuperclass, 419, 60),
        new("RubyDucks", Constants.TeamRubyDucks, 420, 70),
        new("Spearhead", Constants.TeamSpearhead, 418, 60)
    ];

    public string Description => "Export to a Google Sheet the last 12 months of sprint velocity and performance metrics.";

    public string Key => TaskKey;

    public async Task ExecuteAsync(string[] args)
    {
        // Get sprints...
        // https://javlnsupport.atlassian.net/rest/api/3/
        // https://javlnsupport.atlassian.net/rest/agile/1.0/
        // From this API call: {{base_url2}}/board/419/sprint?state=active

        foreach (var team in this.teams)
        {
            team.CurrentSprintId = 2063;
            await ProcessSprint(team);
        }
    }

    private async Task ProcessSprint(TeamSprint teamSprint)
    {
        var result = await greenHopperClient.GetSprintReportAsync(teamSprint.BoardId, teamSprint.CurrentSprintId);
        Console.WriteLine(result);
        if (result is null)
        {
            Console.WriteLine("No sprint report returned from API.");
            return;
        }

        var contents = result["contents"] ?? throw new NotSupportedException("No contents returned from API.");
        var ticketsCompletedOriginalEstimate = contents["completedIssuesInitialEstimateSum"]?.GetValue<double>() ?? 0.0;
        var ticketsCompleted = contents["completedIssuesEstimateSum"]?.GetValue<double>() ?? 0.0;
        var ticketsNotCompleted = contents["issuesNotCompletedInitialEstimateSum"]?.GetValue<double>() ?? 0;
        var ticketsRemoved = contents["puntedIssuesEstimateSum"]?.GetValue<double>() ?? 0;
        var ticketsCompletedOutsideSprint = contents["issuesCompletedInAnotherSprintEstimateSum"]?.GetValue<double>() ?? 0;
        var capacityAccuracy = (ticketsCompleted + ticketsCompletedOutsideSprint) / (ticketsCompletedOriginalEstimate + ticketsNotCompleted + ticketsRemoved);
        var percentOfMaxCapacity = (ticketsCompleted + ticketsCompletedOutsideSprint) / teamSprint.MaxCapacity;

        var sprint = result["sprint"] ?? throw new NotSupportedException("No sprint returned from API.");
        var sprintName = sprint["name"]?.GetValue<string>() ?? string.Empty;
        var sprintStartDate = sprint["startDate"]?.GetValue<DateTimeOffset>() ?? DateTimeOffset.MinValue;
        var sprintState = sprint["state"]?.GetValue<string>() ?? string.Empty;
        var sprintEndDate = sprint["endDate"]?.GetValue<DateTimeOffset>() ?? DateTimeOffset.MinValue;
        var sprintDaysRemaining = sprint["daysRemaining"]?.GetValue<int>() ?? 0;

        Console.WriteLine($"{teamSprint.Team}");
        Console.WriteLine($"Sprint: {sprintName} ({sprintState}), Days remaining: {sprintDaysRemaining}");
        Console.WriteLine($"{sprintStartDate:d-MMM} to {sprintEndDate:d-MMM}");
        Console.WriteLine($"Max theoretical capacity:            {teamSprint.MaxCapacity}");
        Console.WriteLine($"Committed days work:                 {ticketsCompletedOriginalEstimate + ticketsNotCompleted + ticketsRemoved}");
        Console.WriteLine($"Completed days work:                 {ticketsCompleted + ticketsCompletedOutsideSprint}");
        Console.WriteLine($"Capacity accuracy:                   {capacityAccuracy:P0}");
        Console.WriteLine($"Percent of theoretical max capacity: {percentOfMaxCapacity:P0}");
    }

    private record TeamSprint(string Team, string TeamId, int BoardId, double MaxCapacity)
    {
        public int CurrentSprintId { get; set; }
    }
}
