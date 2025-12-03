namespace BensEngineeringMetrics.Tasks;

public class ExportBugStatsTask(BugStatsWorker worker) : IEngineeringMetricsTask
{
    // JAVPM Bug Analysis
    private const string JavPmGoogleSheetId = "16bZeQEPobWcpsD8w7cI2ftdSoT1xWJS8eu41JTJP-oI";
    private const string OtPmGoogleSheetId = "14Dqa1UVXQJrAViBHgbS8kHBmHi61HnkZAKa6wCsTL2E";
    private const string KeyString = "BUG_STATS";

    public string Key => KeyString;
    public string Description => "Export a series of exports summarising _bug_stats_ for JAVPM and OTPM.";

    public async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine($"{Key} - {Description}");
        Console.WriteLine($"--------------------- {Constants.JavPmJiraProjectKey} ---------------------");
        await worker.UpdateSheet(Constants.JavPmJiraProjectKey, JavPmGoogleSheetId);
        Console.WriteLine($"--------------------- {Constants.OtPmJiraProjectKey} ---------------------");
        await worker.UpdateSheet(Constants.OtPmJiraProjectKey, OtPmGoogleSheetId);
    }
}
