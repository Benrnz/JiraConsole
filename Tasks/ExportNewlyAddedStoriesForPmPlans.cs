namespace BensJiraConsole.Tasks;

// ReSharper disable once UnusedType.Global
public class ExportNewlyAddedStoriesForPmPlans : IJiraExportTask
{
    public string Key => "PMPLAN_NEW";
    public string Description => "Export all newly added stories for a time period that map to PMPLANs";

    public async Task ExecuteAsync(string[] fields)
    {
        Console.WriteLine(Description);
        var parentTask = new ExportPmPlanMapping();
        var issues = await parentTask.RetrieveAllStoriesMappingToPmPlan("AND created >= 2025-07-01 AND created < 2025-08-01");
        
        Console.WriteLine($"Found {issues.Values.Count} unique stories");
        if (issues.Values.Count < 20)
        {
            issues.Values.ToList().ForEach(i => Console.WriteLine($"{i.key}"));
        }
        
        var exporter = new SimpleCsvExporter();
        exporter.Export(issues.Values);
    }
}