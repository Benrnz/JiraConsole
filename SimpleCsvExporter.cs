using System.Dynamic;
using System.Text;

namespace BensJiraConsole;

public class SimpleCsvExporter
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

        var propertyNames = GetAllPropertyNames(issues);
        writer.WriteLine(string.Join(',', propertyNames));

        for (var i = 0; i < issues.Count(); i++)
        {
            var record = ReadAllValues(issues.ElementAt(i), propertyNames);
            writer.WriteLine(record);
        }
    }

    private string ReadAllValues(dynamic issue, HashSet<string> propertyNames)
    {
        var sb = new StringBuilder();
        foreach (var propertyName in propertyNames)
        {
            if (issue is ExpandoObject expando && ((IDictionary<string, object>)expando).TryGetValue(propertyName, out var value))
            {
                if (value is ExpandoObject nestedExpando)
                {
                    sb.Append(ReadAllValues(nestedExpando, propertyNames));
                }
                else
                {
                    if (value is string stringValue)
                    {
                        sb.Append("\"");
                        sb.Append(stringValue);
                        sb.Append("\"");
                    }
                    else
                    {
                        sb.Append(value?.ToString() ?? string.Empty);
                    }
                }
            }

            sb.Append(",");
        }

        return sb.ToString().TrimEnd(',');
    }

    private HashSet<string> GetAllPropertyNames(IEnumerable<dynamic> issues)
    {
        var propertyNames = new HashSet<string>();
        foreach (var issue in issues)
        {
            if (issue is ExpandoObject expando)
            {
                foreach (var kvp in (IDictionary<string, object>)expando)
                {
                    propertyNames.Add(kvp.Key);
                }
            }
        }

        return propertyNames;
    }
}
