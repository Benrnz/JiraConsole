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

        foreach (var team in this.teams)
        {
            var sprint = await runner.GetCurrentSprint(team.BoardId);
            if (sprint is null)
            {
                Console.WriteLine("No active sprint for team {team.Team} found.");
                continue;
            }

            team.CurrentSprintId = sprint.Id;
            await ProcessSprint(team);
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

    private async Task ProcessSprint(TeamSprint teamSprint)
    {
        var result = await greenHopperClient.GetSprintReportAsync(teamSprint.BoardId, teamSprint.CurrentSprintId);
        if (result is null)
        {
            Console.WriteLine("No sprint report returned from API.");
            return;
        }

        var contents = result["contents"] ?? throw new NotSupportedException("No contents returned from API.");
        var ticketsCompletedOriginalEstimate = GetValueAsDays(contents["completedIssuesInitialEstimateSum"]);
        var ticketsCompleted = GetValueAsDays(contents["completedIssuesEstimateSum"]);
        var ticketsNotCompleted = GetValueAsDays(contents["issuesNotCompletedInitialEstimateSum"]);
        var ticketsRemoved = GetValueAsDays(contents["puntedIssuesEstimateSum"]);
        var ticketsCompletedOutsideSprint = GetValueAsDays(contents["issuesCompletedInAnotherSprintEstimateSum"]);
        var capacityAccuracy = (ticketsCompleted + ticketsCompletedOutsideSprint) / (ticketsCompletedOriginalEstimate + ticketsNotCompleted + ticketsRemoved);
        var percentOfMaxCapacity = (ticketsCompleted + ticketsCompletedOutsideSprint) / teamSprint.MaxCapacity;

        var sprint = result["sprint"] ?? throw new NotSupportedException("No sprint returned from API.");
        var sprintName = sprint["name"]?.GetValue<string>() ?? string.Empty;
        var sprintStartDate = GetDateTimeOffset(sprint["startDate"]);
        var sprintState = sprint["state"]?.GetValue<string>() ?? string.Empty;
        var sprintEndDate = GetDateTimeOffset(sprint["endDate"]);
        var sprintDaysRemaining = sprint["daysRemaining"]?.GetValue<int>() ?? 0;

        Console.WriteLine($"{teamSprint.Team}");
        Console.WriteLine($"Sprint: {sprintName} ({sprintState}), Days remaining: {sprintDaysRemaining}");
        Console.WriteLine($"{sprintStartDate:d-MMM} to {sprintEndDate:d-MMM}");
        Console.WriteLine($"Max theoretical capacity:            {teamSprint.MaxCapacity}");
        Console.WriteLine($"Committed days work:                 {ticketsCompletedOriginalEstimate + ticketsNotCompleted + ticketsRemoved}");
        Console.WriteLine($"Completed days work:                 {ticketsCompleted + ticketsCompletedOutsideSprint}");
        Console.WriteLine($"Capacity accuracy:                   {capacityAccuracy:P0}");
        Console.WriteLine($"Percent of theoretical max capacity: {percentOfMaxCapacity:P0}");
        Console.WriteLine("------------------------------------------------------------------------");
    }

    private record TeamSprint(string Team, string TeamId, int BoardId, double MaxCapacity)
    {
        public int CurrentSprintId { get; set; }
    }
}
