using System.Text.Json.Nodes;

namespace BensJiraConsole.Tasks;

public class SprintVelocityAndPerformanceTask(IGreenHopperClient greenHopperClient, IJiraQueryRunner runner, IWorkSheetReader reader, IWorkSheetUpdater updater) : IJiraExportTask
{
    private const string GoogleSheetId = "1HuI-uYOtR66rs8B0qp8e3L39x13reFTaiOB3VN42vAQ";
    private const string TaskKey = "SPRINT_PERF";

    private readonly TeamSprint[] teams =
    [
        new("Superclass", Constants.TeamSuperclass, 419, 60),
        new("RubyDucks", Constants.TeamRubyDucks, 420, 70),
        new("Spearhead", Constants.TeamSpearhead, 418, 60),
        new("Officetech", Constants.TeamOfficetech, 483, 35)
    ];

    public string Description => "Export to a Google Sheet the last 12 months of sprint velocity and performance metrics.";

    public string Key => TaskKey;

    public async Task ExecuteAsync(string[] args)
    {
        // This task can accept a list of space seperated sprint IDs from the commandline to extract metrics for those sprints. Omit for current sprint.
        Console.WriteLine(Description);
        Console.WriteLine();

        var sprintsOfInterest = await ParseOptionalArguments(args);

        var sprintMetrics = await ExtractAndCalculateSprintMetrics(sprintsOfInterest);

        await UpdateSheet(sprintMetrics);
    }

    private async Task UpdateSheet(IReadOnlyList<SprintMetrics> sprintMetrics)
    {
        await reader.Open(GoogleSheetId);
        var row = new List<object?>();
        foreach (var sprintMetric in sprintMetrics)
        {
            row.Add($"{sprintMetric.SprintName} {sprintMetric.StartDate:d-MMM-yy} to {sprintMetric.EndDate:d-MMM-yy}");
            row.Add(sprintMetric.CommittedDaysWork);
            row.Add(sprintMetric.CompletedDaysWork);
            row.Add(sprintMetric.CapacityAccuracy);
            row.Add(sprintMetric.PercentOfMaxCapacity);
        };

        var sheetData = new List<IList<object?>> { row };

        var lastRow = await reader.GetLastRowInColumnAsync("Summary", "A");

        await updater.Open(GoogleSheetId);
        updater.EditSheet($"'Summary'!A{lastRow + 1}", sheetData);
        updater.EditSheet("Info!B1", [[DateTime.Now.ToString("g")]]);
        await updater.SubmitBatch();
    }

    private async Task<IReadOnlyList<SprintMetrics>> ExtractAndCalculateSprintMetrics(List<AgileSprint> sprintsOfInterest)
    {
        var sprintMetrics = new List<SprintMetrics>();
        foreach (var team in this.teams)
        {
            if (sprintsOfInterest.Any())
            {
                var sprint = sprintsOfInterest.FirstOrDefault(x => x.OriginBoardId == team.BoardId);
                if (sprint is null)
                {
                    // This team doesn't have a sprint in the requested list.
                    continue;
                }

                team.CurrentSprintId = sprint.Id;
            }
            else
            {
                var sprint = await runner.GetCurrentSprintForBoard(team.BoardId);
                if (sprint is null)
                {
                    Console.WriteLine("No active sprint for team {team.Team} found.");
                    continue;
                }

                team.CurrentSprintId = sprint.Id;
            }

            sprintMetrics.Add(await ProcessSprint(team));
        }

        return sprintMetrics;
    }

    private async Task<List<AgileSprint>> ParseOptionalArguments(string[] args)
    {
        var sprintsOfInterest = new List<AgileSprint>();
        foreach (var requestedSprint in args.Skip(1).Select(int.Parse))
        {
            var sprint = await runner.GetSprintById(requestedSprint);
            if (sprint is null)
            {
                Console.WriteLine($"No such sprint found: {requestedSprint}.");
                return sprintsOfInterest;
            }

            sprintsOfInterest.Add(sprint);
        }

        return sprintsOfInterest;
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
        var ticketsCompletedInitialEstimate = GetValueAsDays(contents["completedIssuesInitialEstimateSum"]);
        var ticketsCompleted = GetValueAsDays(contents["completedIssuesEstimateSum"]);
        var ticketsNotCompletedInitial = GetValueAsDays(contents["issuesNotCompletedInitialEstimateSum"]);
        var ticketsRemovedInitial = GetValueAsDays(contents["puntedIssuesInitialEstimateSum"]);
        var ticketsCompletedOutsideSprint = GetValueAsDays(contents["issuesCompletedInAnotherSprintEstimateSum"]);
        var sprint = result["sprint"] ?? throw new NotSupportedException("No sprint returned from API.");

        var sprintMetrics = new SprintMetrics(
            teamSprint,
            sprint["name"]?.GetValue<string>() ?? string.Empty,
            sprint["state"]?.GetValue<string>() ?? string.Empty,
            sprint["daysRemaining"]?.GetValue<int>() ?? 0,
            GetDateTimeOffset(sprint["startDate"]),
            GetDateTimeOffset(sprint["endDate"]),
            ticketsCompletedInitialEstimate + ticketsNotCompletedInitial + ticketsRemovedInitial,
            ticketsCompleted + ticketsCompletedOutsideSprint,
            (ticketsCompleted + ticketsCompletedOutsideSprint) / (ticketsCompletedInitialEstimate + ticketsNotCompletedInitial + ticketsRemovedInitial),
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
