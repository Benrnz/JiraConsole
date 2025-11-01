namespace BensJiraConsole.Tasks;

public class SprintVelocityAndPerformanceTask(IGreenHopperRunner greenHopperRunner) : IJiraExportTask
{
    private const string GoogleSheetId = "";
    private const string TaskKey = "SPRINT_PERF";

    private record TeamSprint(string Team, string TeamId, int BoardId);

    private TeamSprint[] Teams = [new TeamSprint("Superclass", "", 419)];

    public string Description => "Export to a Google Sheet the last 12 months of sprint velocity and performance metrics.";

    public string Key => TaskKey;

    public async Task ExecuteAsync(string[] args)
    {
        // https://javlnsupport.atlassian.net/rest/greenhopper/1.0/rapid/charts/sprintreport?rapidViewId=419&sprintId=2063
        // rapidViewId is boardID. Superclass for example is board id 419

        await ProcessSprint(419, 2063);
    }

    private async Task ProcessSprint(int sprintBoardId, int sprintId)
    {
        var result = await greenHopperRunner.GetSprintReportAsync(sprintBoardId, sprintId);
        Console.WriteLine(result);
        if (result is null)
        {
            Console.WriteLine("No sprint report returned from API.");
            return;
        }

        var contents = result["contents"] ?? throw new NotSupportedException("No contents returned from API.");
        var ticketsCompletedOriginalEstimate = contents["completedIssuesInitialEstimateSum"]?.GetValue<double>() ?? 0.0;
        var ticketsNotCompleted = contents["issuesNotCompletedEstimateSum"]?.GetValue<double>() ?? 0;
        var ticketsRemoved = contents["puntedIssuesEstimateSum"]?.GetValue<double>() ?? 0;
        var ticketsCompletedOutsideSprint = contents["issuesCompletedInAnotherSprintEstimateSum"]?.GetValue<double>() ?? 0;

        var sprint = result["sprint"] ?? throw new NotSupportedException("No sprint returned from API.");
        var sprintName = sprint["name"]?.GetValue<string>() ?? string.Empty;
        var sprintStartDate = sprint["startDate"]?.GetValue<DateTimeOffset>() ?? DateTimeOffset.MinValue;
        var sprintState = sprint["state"]?.GetValue<string>() ?? string.Empty;
        var sprintDaysRemaining = sprint["daysRemaining"]?.GetValue<int>() ?? 0;

    }
}
