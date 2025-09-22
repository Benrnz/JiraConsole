namespace BensJiraConsole.Tasks;

public class InitiativeAggregateTableAndBurnup : IJiraExportTask
{
    public string Description => "Run both INIT_TABLE and INIT_BURNUP";
    public string Key => "INIT_ALL";

    public async Task ExecuteAsync(string[] args)
    {
        var mainTask = new InitiativeProgressTableTask();
        await mainTask.ExecuteAsync(args);

        var burnupChartTask = new InitiativeBurnUpsTask();
        await burnupChartTask.ExecuteAsync(mainTask, args);
    }
}
