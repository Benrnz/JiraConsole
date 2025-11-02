using System.Text.Json.Nodes;

namespace BensJiraConsole.Tasks;

public class SprintVelocityAndPerformanceTask(IGreenHopperClient greenHopperClient, IJiraQueryRunner runner) : IJiraExportTask
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
        Console.WriteLine(Description);
        Console.WriteLine();

        var sprintMetrics = new List<SprintMetrics>();
        foreach (var team in this.teams)
        {
            var sprint = await runner.GetCurrentSprint(team.BoardId);
            if (sprint is null)
            {
                Console.WriteLine("No active sprint for team {team.Team} found.");
                continue;
            }

            team.CurrentSprintId = sprint.Id;
            sprintMetrics.Add(await ProcessSprint(team));
        }
    }

    private DateTimeOffset GetDateTimeOffset(JsonNode? node)
    {
        var stringDate = node?.GetValue<string>();
        if (stringDate is null)
        {
            return DateTimeOffset.MinValue;
        }

        return DateTimeOffset.Parse(stringDate);
    }

    private double GetValueAsDays(JsonNode? node)
    {
        var value = node?["value"]?.GetValue<double>() ?? 0.0;
        return value / 60 / 60 / 8; // values were in seconds, convert to days, 8 hours in a day.
    }

    private async Task<SprintMetrics> ProcessSprint(TeamSprint teamSprint)
    {
        var result = await greenHopperClient.GetSprintReportAsync(teamSprint.BoardId, teamSprint.CurrentSprintId);
        if (result is null)
        {
            Console.WriteLine("No sprint report returned from API.");
            return new SprintMetrics(teamSprint, "No sprint found", "NOT-FOUND", 0, DateTimeOffset.MinValue, DateTimeOffset.MinValue, 0, 0, 0, 0);
        }

        var contents = result["contents"] ?? throw new NotSupportedException("No contents returned from API.");
        var ticketsCompletedOriginalEstimate = GetValueAsDays(contents["completedIssuesInitialEstimateSum"]);
        var ticketsCompleted = GetValueAsDays(contents["completedIssuesEstimateSum"]);
        var ticketsNotCompleted = GetValueAsDays(contents["issuesNotCompletedInitialEstimateSum"]);
        var ticketsRemoved = GetValueAsDays(contents["puntedIssuesEstimateSum"]);
        var ticketsCompletedOutsideSprint = GetValueAsDays(contents["issuesCompletedInAnotherSprintEstimateSum"]);
        var sprint = result["sprint"] ?? throw new NotSupportedException("No sprint returned from API.");

        var sprintMetrics = new SprintMetrics(
            teamSprint,
            sprint["name"]?.GetValue<string>() ?? string.Empty,
            sprint["state"]?.GetValue<string>() ?? string.Empty,
            sprint["daysRemaining"]?.GetValue<int>() ?? 0,
            GetDateTimeOffset(sprint["startDate"]),
            GetDateTimeOffset(sprint["endDate"]),
            ticketsCompletedOriginalEstimate + ticketsNotCompleted + ticketsRemoved,
            ticketsCompleted + ticketsCompletedOutsideSprint,
            (ticketsCompleted + ticketsCompletedOutsideSprint) / (ticketsCompletedOriginalEstimate + ticketsNotCompleted + ticketsRemoved),
            (ticketsCompleted + ticketsCompletedOutsideSprint) / teamSprint.MaxCapacity);

        Console.WriteLine($"{sprintMetrics.Team.Team}");
        Console.WriteLine($"Sprint: {sprintMetrics.SprintName} ({sprintMetrics.SprintState}), Days remaining: {sprintMetrics.DaysRemaining}");
        Console.WriteLine($"{sprintMetrics.StartDate:d-MMM} to {sprintMetrics.EndDate:d-MMM}");
        Console.WriteLine($"Max theoretical capacity:            {sprintMetrics.Team.MaxCapacity}");
        Console.WriteLine($"Committed days work:                 {sprintMetrics.CommittedDaysWork}");
        Console.WriteLine($"Completed days work:                 {sprintMetrics.CompletedDaysWork}");
        Console.WriteLine($"Capacity accuracy:                   {sprintMetrics.CapacityAccuracy:P0}");
        Console.WriteLine($"Percent of theoretical max capacity: {sprintMetrics.PercentOfMaxCapacity:P0}");
        Console.WriteLine("------------------------------------------------------------------------");

        return sprintMetrics;
    }

    private record SprintMetrics(
        TeamSprint Team,
        string SprintName,
        string SprintState,
        int DaysRemaining,
        DateTimeOffset StartDate,
        DateTimeOffset EndDate,
        double CommittedDaysWork,
        double CompletedDaysWork,
        double CapacityAccuracy,
        double PercentOfMaxCapacity);

    private record TeamSprint(string Team, string TeamId, int BoardId, double MaxCapacity)
    {
        public int CurrentSprintId { get; set; }
    }
}
