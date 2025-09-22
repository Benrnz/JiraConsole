using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BensJiraConsole;

public class App(IEnumerable<IJiraExportTask> tasks)
{
    private readonly IJiraExportTask[] allTasks = tasks.ToArray();
    private string[] commandLineArgs = [];

    public async Task Run(string[] args)
    {
        this.commandLineArgs = args;
        await ExecuteMode(args.Length > 0 ? args[0] : "NOT_SET");
    }

    private async Task ExecuteMode(string? mode)
    {
        IJiraExportTask? selectedTask = null;
        StringBuilder help = new();
        for (var index = 0; index < tasks.Count(); index++)
        {
            help.AppendLine($"{index + 1}: {this.allTasks[index].Description} ({this.allTasks[index].Key})");
            if (mode == this.allTasks[index].Key || mode == (index + 1).ToString())
            {
                selectedTask = this.allTasks[index];
            }
        }

        if (mode == "NOT_SET")
        {
            Console.WriteLine(help);
            Console.WriteLine("Type 'exit' to quit.");
            await Console.Out.FlushAsync();
            mode = Console.ReadLine();
            await ExecuteMode(mode);
            return;
        }

        if (selectedTask is null)
        {
            return;
        }

        await selectedTask.ExecuteAsync(this.commandLineArgs);
    }
}
