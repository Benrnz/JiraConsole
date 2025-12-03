using System.Globalization;

namespace BensEngineeringMetrics.Tasks;

// ReSharper disable once UnusedType.Global
public class ExportNewlyAddedStoriesForPmPlans(ICloudUploader uploader, ICsvExporter exporter, ExportPmPlanStories pmPlanStoriesTask) : IEngineeringMetricsTask
{
    private const string KeyString = "PMPLAN_NEW";

    public string Key => KeyString;
    public string Description => "Export all _newly_ added stories for a time period that map to PMPLANs";

    public async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine($"{Key} - {Description}");

        var startDate = GetDateFromUser("start date (inclusive)");
        var endDate = GetDateFromUser("end date (exclusive)");
        var issues = await pmPlanStoriesTask.RetrieveAllStoriesMappingToPmPlan($"AND created >= {startDate:yyyy-MM-dd} AND Created < {endDate:yyyy-MM-dd}");

        Console.WriteLine($"Found {issues.Count} unique stories");
        if (issues.Count < 20)
        {
            issues.ToList().ForEach(i => Console.WriteLine($"{i.Key}"));
        }

        exporter.SetFileNameMode(FileNameMode.Hint, Key);
        var filename = exporter.Export(issues);
        await uploader.UploadCsvAsync(filename, Path.GetFileName(filename));
    }

    private DateTime GetDateFromUser(string dateDescription)
    {
        string? input;
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
