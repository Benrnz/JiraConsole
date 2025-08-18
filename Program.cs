using System.Reflection;
using System.Text;
using BensJiraConsole;

public static class Program
{
    private static string ProvidedFileName = string.Empty;

    private static string[] PreferredFields { get; } =
    [
        "summary",
        "status",
        "issuetype",
        "customfield_11934", // Dev Time Spent
        "parent",
        "customfield_10004", // Story Points
        "created",
        "assignee"
    ];

    public static async Task Main(string[] args)
    {
        Console.WriteLine("Jira Console Exporter tool.  Select a task to execute, or 'exit' to quit.");
        var tasks = FindExportTaskImplementations();
        if (args.Length >= 2)
        {
            ProvidedFileName = args[1];
            var invalidChars = Path.GetInvalidFileNameChars();
            if (!ProvidedFileName.Any(c => invalidChars.Contains(c)))
            {
                Console.WriteLine($"ERROR: Invalid filename '{ProvidedFileName}' provided.");
            }
        }

        await ExecuteMode(args.Length > 0 ? args[0] : "NOT_SET", tasks);

        Console.WriteLine("Exiting.");
    }


    private static async Task ExecuteMode(string? mode, IJiraExportTask[] tasks)
    {
        IJiraExportTask? selectedTask = null;
        StringBuilder help = new();
        for (var index = 0; index < tasks.Count(); index++)
        {
            help.AppendLine($"{index + 1}: {tasks[index].Description} ({tasks[index].Key})");
            if (mode == tasks[index].Key || mode == (index + 1).ToString())
            {
                selectedTask = tasks[index];
            }
        }

        if (mode == "NOT_SET")
        {
            Console.WriteLine(help);
            mode = Console.ReadLine();
            await ExecuteMode(mode, tasks);
            return;
        }

        if (selectedTask is null)
        {
            return;
        }

        await selectedTask.ExecuteAsync(PreferredFields);
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
