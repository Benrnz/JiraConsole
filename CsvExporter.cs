using System.Globalization;
using CsvHelper;

namespace BensJiraConsole;

public class CsvExporter
{
    private const string DefaultFolder = "C:\\Downloads\\JiraExports";

    public string Export(IEnumerable<object> issues)
    {
        var fileName = $"{DefaultFolder}\\BensJiraConsole-{DateTime.Now:yyyyMMddHHmmss}.csv";
        WriteCsv(fileName, issues);
        return fileName;
    }

    private void WriteCsv(string path, IEnumerable<object> issues)
    {
        if (!Path.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        }

        using var writer = new StreamWriter(path);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.WriteRecords(issues);
    }
}
