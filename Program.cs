using System.Reflection;
using System.Text;
using BensJiraConsole;

public static class Program
{
    private static string[] PreferredFields { get; } =
    [
        "summary",
        "status",
        "issuetype",
        "customfield_11934", // Dev Time Spent
        "parent",
        "customfield_10004", // Story Points
        "created"
    ];


    public static async Task Main(string[] args)
    {
        var tasks = FindExportTaskImplementations();
        var issues = await ExecuteMode(args.Length > 0 ? args[0] : "NOT_SET", tasks);

        var exporter = new CsvExporter();
        var fileName = exporter.Export(issues);
        Console.WriteLine("Export completed!");
        Console.WriteLine(Path.GetFullPath(fileName));
    }


    private static async Task<List<JiraIssue>> ExecuteMode(string? mode, IJiraExportTask[] tasks)
    {
        IJiraExportTask? selectedTask = null;
        StringBuilder help = new();
        for (var index = 0; index < tasks.Count(); index++)
        {
            help.AppendLine($"{index + 1}: {tasks[index].Description}");
            if (mode == tasks[index].Key || mode == (index + 1).ToString())
            {
                selectedTask = tasks[index];
            }
        }

        if (mode == "NOT_SET")
        {
            Console.WriteLine(help);
            mode = Console.ReadLine();
            return await ExecuteMode(mode, tasks);
        }

        if (selectedTask is null)
        {
            return new List<JiraIssue>();
        }

        return await selectedTask.ExecuteAsync(PreferredFields);
    }

    private static IJiraExportTask[] FindExportTaskImplementations()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var types = assembly.GetTypes()
            .Where(type => typeof(IJiraExportTask).IsAssignableFrom(type)
                           && type is { IsInterface: false, IsAbstract: false })
            .ToList();
        return types.Select(Activator.CreateInstance).Cast<IJiraExportTask>().ToArray();
    }
}
