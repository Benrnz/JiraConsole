namespace BensJiraConsole.Tasks;

public class InitiativeAggregateTableAndBurnup(InitiativeProgressTableTask tableTask, InitiativeBurnUpsTask burnUpTask) : IJiraExportTask
{
    public string Description => "Run both INIT_TABLE and INIT_BURNUP";
    public string Key => "INIT_ALL";

    public async Task ExecuteAsync(string[] args)
    {
        await tableTask.ExecuteAsync(args);

        await burnUpTask.ExecuteAsync(tableTask, args);
    }
}
