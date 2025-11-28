using System.Dynamic;
using System.Reflection;
using System.Text;

namespace BensJiraConsole;

public class SimpleCsvExporter : ICsvExporter
{
    private const string DefaultFolder = "C:\\Downloads\\JiraExports";

    private FileNameMode fileNameMode = FileNameMode.Hint;

    private string nameHint = Assembly.GetCallingAssembly().FullName!;

    public void SetFileNameMode(FileNameMode mode, string fileNameHint)
    {
        this.fileNameMode = mode;
        this.nameHint = fileNameHint;
    }

    public string Export(IEnumerable<object> issues, Func<string>? overrideSerialiseHeader = null, Func<object, string>? overrideSerialiseRecord = null)
    {
        var data = issues.ToList();
        if (!data.Any())
        {
            Console.WriteLine("No data to export.");
            return string.Empty;
        }

        string fileName;
        switch (this.fileNameMode)
        {
            case FileNameMode.ExactName:
                fileName = this.nameHint ?? throw new ArgumentNullException(nameof(this.nameHint));
                break;
            default:
                fileName = $"{this.nameHint}-{DateTime.Now:yyyyMMddHHmmss}";
                break;
        }

        var pathAndFileName = $"{DefaultFolder}\\{fileName}.csv";
        WriteCsv(pathAndFileName, data, overrideSerialiseHeader, overrideSerialiseRecord);
        Console.WriteLine(Path.GetFullPath(pathAndFileName));
        return pathAndFileName;
    }

    private HashSet<string> GetAllPropertyNames(IList<object> issues, out bool isDynamic)
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
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
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
                foreach (var kvp in (IDictionary<string, object>)expando!)
                {
                    propertyNames.Add(kvp.Key);
                }
            }
        }

        return propertyNames;
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
                sb.Append(Math.Round(doubleValue, 3));
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
                sb.Append(value);
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
            if (issue is ExpandoObject expando && ((IDictionary<string, object>)expando!).TryGetValue(propertyName, out var value))
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
                        sb.Append(SanitiseString(stringValue));
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

    private string SanitiseString(string? rawString)
    {
        if (rawString is null)
        {
            return string.Empty;
        }

        return rawString.Replace('"', '\'').Replace(',', ';');
    }

    private void WriteCsv(string path, IList<object> issues, Func<string>? overrideSerialiseHeader, Func<object, string>? overrideSerialiseRecord)
    {
        if (!Path.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        }

        using var writer = new StreamWriter(path);

        var propertyNames = GetAllPropertyNames(issues, out var isDynamic);

        // Write Header names
        if (overrideSerialiseHeader == null)
        {
            writer.WriteLine(string.Join(',', propertyNames));
        }
        else
        {
            writer.WriteLine(overrideSerialiseHeader());
        }

        for (var i = 0; i < issues.Count(); i++)
        {
            string record;
            if (overrideSerialiseRecord == null)
            {
                record = isDynamic ? ReadAllValuesDynamic(issues.ElementAt(i), propertyNames) : ReadAllValues(issues.ElementAt(i), propertyNames);
            }
            else
            {
                record = overrideSerialiseRecord(issues.ElementAt(i));
            }

            writer.WriteLine(record);
        }
    }
}
