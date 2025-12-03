using System.Text.Json.Nodes;
using BensEngineeringMetrics.Jira;

namespace BensEngineeringMetrics.Tasks;

public class SprintVelocityAndPerformanceTask(IGreenHopperClient greenHopperClient, IJiraQueryRunner runner, IWorkSheetReader reader, IWorkSheetUpdater updater) : IJiraExportTask
{
    private const string GoogleSheetId = "1HuI-uYOtR66rs8B0qp8e3L39x13reFTaiOB3VN42vAQ";
    private const string TaskKey = "SPRINT_PERF";

    public string Description => "Export to a Google Sheet the last 12 months of sprint velocity and performance metrics.";

    public string Key => TaskKey;

    public async Task ExecuteAsync(string[] args)
    {
        // This task can accept a list of space seperated sprint IDs from the commandline to extract metrics for those sprints. Omit for current sprint.
        Console.WriteLine(Description);
        Console.WriteLine();

        var sprintsOfInterest = await ParseArguments(args);

        var sprintMetrics = await ExtractAndCalculateSprintMetrics(sprintsOfInterest);

        await UpdateSheet(sprintMetrics);
    }

    private async Task<IReadOnlyList<SprintMetrics>> ExtractAndCalculateSprintMetrics(List<AgileSprint> sprintsOfInterest)
    {
        var sprintMetrics = new List<SprintMetrics>();
        foreach (var team in JiraConfig.Teams)
        {
            TeamSprint teamSprint;
            if (sprintsOfInterest.Any())
            {
                var sprint = sprintsOfInterest.FirstOrDefault(x => x.BoardId == team.BoardId);
                if (sprint is null)
                {
                    // This team doesn't have a sprint in the requested list.
                    continue;
                }

                teamSprint = new TeamSprint(team, sprint.Id);
            }
            else
            {
                Console.WriteLine("ERROR - no sprints specified.");
                return sprintMetrics;
            }

            sprintMetrics.Add(await ProcessSprint(teamSprint));
        }

        return sprintMetrics;
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

    private async Task<List<AgileSprint>> ParseArguments(string[] args)
    {
        var providedSprintNumbers = args.Skip(1).Select(int.Parse).ToList();
        var suggestedSprints = string.Empty;
        while (!providedSprintNumbers.Any())
        {
            if (string.IsNullOrEmpty(suggestedSprints))
            {
                foreach (var team in JiraConfig.Teams)
                {
                    var sprint = await runner.GetCurrentSprintForBoard(team.BoardId);
                    if (sprint is null)
                    {
                        continue;
                    }

                    suggestedSprints += $"{team.TeamName}:{sprint.Id} ";
                }
            }

            Console.WriteLine("Enter space seperated sprint numbers you would like to analyse: (eg: 2065 2066)");
            Console.WriteLine($"Current sprints for teams are: {suggestedSprints}");
            var input = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(input))
            {
                providedSprintNumbers = input.Split(' ').Select(int.Parse).ToList();
            }
        }

        var sprintsOfInterest = new List<AgileSprint>();
        foreach (var requestedSprint in providedSprintNumbers)
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

    private async Task UpdateSheet(IReadOnlyList<SprintMetrics> sprintMetrics)
    {
        await reader.Open(GoogleSheetId);
        var row = new List<object?>();
        foreach (var sprintMetric in sprintMetrics)
        {
            var sprintText = $"{sprintMetric.SprintName} {sprintMetric.StartDate:d-MMM-yy} to {sprintMetric.EndDate:d-MMM-yy}";
            row.Add(
                $"""=HYPERLINK("https://javlnsupport.atlassian.net/jira/software/c/projects/JAVPM/boards/{sprintMetric.Team.BoardId}/reports/sprint-retrospective?sprint={sprintMetric.Team.CurrentSprintId}", "{sprintText}")""");
            row.Add(sprintMetric.CommittedDaysWork);
            row.Add(sprintMetric.CompletedDaysWork);
            row.Add(sprintMetric.CapacityAccuracy);
            row.Add(sprintMetric.PercentOfMaxCapacity);
        }

        var sheetData = new List<IList<object?>> { row };

        var lastRow = await reader.GetLastRowInColumnAsync("Summary", "A");

        await updater.Open(GoogleSheetId);
        updater.EditSheet($"'Summary'!A{lastRow + 1}", sheetData, true);
        updater.EditSheet("Info!B1", [[DateTime.Now.ToString("g")]]);
        await updater.SubmitBatch();
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

    private record TeamSprint(TeamConfig Team, int CurrentSprintId) : TeamConfig(Team.TeamName, Team.TeamId, Team.BoardId, Team.MaxCapacity, Team.JiraProject);
}
