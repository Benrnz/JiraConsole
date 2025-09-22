using System.Globalization;

namespace BensJiraConsole.Tasks;

// ReSharper disable once UnusedType.Global
public class ExportNewlyAddedStoriesForPmPlans : IJiraExportTask
{
    private const string KeyString = "PMPLAN_NEW";
    private readonly ICsvExporter exporter = new SimpleCsvExporter(KeyString);
    private readonly ICloudUploader uploader = new GoogleDriveUploader();

    public string Key => KeyString;
    public string Description => "Export all _newly_ added stories for a time period that map to PMPLANs";

    public async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine(Description);
        var parentTask = new ExportPmPlanStories();
        var startDate = GetDateFromUser("start date (inclusive)");
        var endDate = GetDateFromUser("end date (exclusive)");
        var issues = await parentTask.RetrieveAllStoriesMappingToPmPlan($"AND created >= {startDate:yyyy-MM-dd} AND Created < {endDate:yyyy-MM-dd}");

        Console.WriteLine($"Found {issues.Count} unique stories");
        if (issues.Count < 20)
        {
            issues.ToList().ForEach(i => Console.WriteLine($"{i.Key}"));
        }

        var filename = this.exporter.Export(issues);
        await this.uploader.UploadCsvAsync(filename, Path.GetFileName(filename));
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
