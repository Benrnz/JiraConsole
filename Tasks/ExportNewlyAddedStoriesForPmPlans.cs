using System.Globalization;

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
        var startDate = GetDateFromUser("start date (inclusive)");
        var endDate = GetDateFromUser("end date (exclusive)");
        var issues = await parentTask.RetrieveAllStoriesMappingToPmPlan($"AND created >= {startDate:yyyy-MM-dd} AND created < {endDate:yyyy-MM-dd}");

        Console.WriteLine($"Found {issues.Values.Count} unique stories");
        if (issues.Values.Count < 20)
        {
            issues.Values.ToList().ForEach(i => Console.WriteLine($"{i.key}"));
        }

        var exporter = new SimpleCsvExporter(Key);
        exporter.Export(issues.Values);
    }

    private DateTime GetDateFromUser(string dateDescription)
    {
        string input;
        do
        {
            Console.WriteLine($"Enter a {dateDescription} (dd-MM-yyyy):");
            input = Console.ReadLine();
            if (DateTime.TryParseExact(input, "dd-MM-yyyy", null, DateTimeStyles.None, out var date))
            {
                return date;
            }

            Console.WriteLine("Invalid date format. Please try again.");
        } while (input != "exit");

        return DateTime.Today;
    }
}
