using System.Collections;
using System.Diagnostics;
using System.Dynamic;
using System.Reflection;
using System.Text;

namespace BensJiraConsole;

public class SimpleCsvExporter(string taskKey)
{
    private const string DefaultFolder = "C:\\Downloads\\JiraExports";

    public enum FileNameMode
    {
        Auto,
        ExactName,
        Hint,
    }

    private readonly string taskKey = taskKey ?? throw new ArgumentNullException(nameof(taskKey));

    public FileNameMode Mode { get; set; } = FileNameMode.Auto;

    public string Export(IEnumerable<object> issues, string? fileNameHint = null)
    {
        if (!issues.Any())
        {
            Console.WriteLine("No data to export.");
            return string.Empty;
        }

        string fileName;
        switch (Mode)
        {
            case FileNameMode.ExactName:
                fileName = fileNameHint ?? throw new ArgumentNullException(nameof(fileNameHint));
                break;
            case FileNameMode.Hint:
                fileName = $"{fileNameHint ?? "BensJiraConsole"}-{DateTime.Now:yyyyMMddHHmmss}";
                break;
            default:
                fileName = $"{this.taskKey}-{DateTime.Now:yyyyMMddHHmmss}";
                break;
        }

        var pathAndFileName = $"{DefaultFolder}\\{fileName}.csv";
        WriteCsv(pathAndFileName, issues);
        Console.WriteLine(Path.GetFullPath(pathAndFileName));
        return pathAndFileName;
    }

    private void WriteCsv(string path, IEnumerable<object> issues)
    {
        if (!Path.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        }

        using var writer = new StreamWriter(path);

        var propertyNames = GetAllPropertyNames(issues, out var isDynamic);
        writer.WriteLine(string.Join(',', propertyNames));

        for (var i = 0; i < issues.Count(); i++)
        {
            var record = isDynamic ? ReadAllValuesDynamic(issues.ElementAt(i), propertyNames) : ReadAllValues(issues.ElementAt(i), propertyNames);
            writer.WriteLine(record);
        }
    }

    private string ReadAllValues(object issue, HashSet<string> propertyNames)
    {
        var sb = new StringBuilder();
        var type = issue.GetType();
        foreach (var propertyName in propertyNames)
        {
            var value = type.GetProperty(propertyName)?.GetValue(issue);
            if (value is string stringValue)
            {
                sb.Append("\"");
                sb.Append(stringValue);
                sb.Append("\"");
            }
            else if (value is double doubleValue)
            {
                sb.Append(Math.Round(doubleValue,3));
            }
            else if (value is DateTimeOffset dateTimeOffset)
            {
                sb.Append(dateTimeOffset.ToLocalTime().ToString("yyyy-MM-ddTHH:mm:ss"));
            }
            else if (value is DateTime dateTime)
            {
                sb.Append(dateTime.ToLocalTime().ToString("yyyy-MM-ddTHH:mm:ss"));
            }
            else if (value is not null)
            {
                sb.Append(value.ToString());
            }
            sb.Append(",");
        }

        return sb.ToString().TrimEnd(',');
    }

    private string ReadAllValuesDynamic(dynamic issue, HashSet<string> propertyNames)
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
                    else if (value is DateTimeOffset dateTimeOffset)
                    {
                        sb.Append(dateTimeOffset.ToLocalTime().ToString("yyyy-MM-ddTHH:mm:ss"));
                    }
                    else if (value is DateTime dateTime)
                    {
                        sb.Append(dateTime.ToLocalTime().ToString("yyyy-MM-ddTHH:mm:ss"));
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

    private HashSet<string> GetAllPropertyNames(IEnumerable<object> issues, out bool isDynamic)
    {
        var first = issues.First();
        if (first is ExpandoObject)
        {
            isDynamic = true;
            return GetAllPropertyNamesDynamic(issues);
        }

        isDynamic = false;
        var propertyNames = new HashSet<string>();
        first
            .GetType()
            .GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            .Select(p => p.Name)
            .ToList()
            .ForEach(p => propertyNames.Add(p));

        return propertyNames;
    }

    private HashSet<string> GetAllPropertyNamesDynamic(IEnumerable<dynamic> issues)
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
