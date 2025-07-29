using System.Globalization;
using CsvHelper;

namespace BensJiraConsole;

public class CsvExporter
{
    private const string DefaultFolder = "C:\\Downloads\\JiraExports";

    public string Export(IEnumerable<JiraIssue> issues)
    {
        var fileName = $"{DefaultFolder}\\BensJiraConsole-{DateTime.Now:yyyyMMddHHmmss}.csv";
        WriteCsv(fileName, issues);
        return fileName;
    }

    private void WriteCsv(string path, IEnumerable<JiraIssue> issues)
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
