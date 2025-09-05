using System.Reflection;
using System.Text;
using BensJiraConsole;

public static class Program
{
    private static string[] CommandLineArgs = [];

    public static async Task Main(string[] args)
    {
        CommandLineArgs = args;
        Console.WriteLine("Jira Console Exporter tool.  Select a task to execute, or 'exit' to quit.");
        var tasks = FindExportTaskImplementations();
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
            Console.WriteLine("Type 'exit' to quit.");
            Console.Out.Flush();
            mode = Console.ReadLine();
            await ExecuteMode(mode, tasks);
            return;
        }

        if (selectedTask is null)
        {
            return;
        }

        await selectedTask.ExecuteAsync(CommandLineArgs);
    }

    private static IJiraExportTask[] FindExportTaskImplementations()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var types = assembly.GetTypes()
            .Where(type => typeof(IJiraExportTask).IsAssignableFrom(type)
                           && type is { IsInterface: false, IsAbstract: false })
            .ToList();
        return types
            .Select(Activator.CreateInstance)
            .Cast<IJiraExportTask>()
            .OrderBy(t => t.Key)
            .ToArray();
    }
}
