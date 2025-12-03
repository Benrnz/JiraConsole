using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;

namespace BensEngineeringMetrics;

public class App(IEnumerable<IJiraExportTask> tasks)
{
    public static readonly HttpClient HttpJira = CreateJiraHttpClient();
    public static readonly HttpClient HttpSlack = CreateSlackHttpClient();

    private readonly IJiraExportTask[] allTasks = tasks.ToArray();
    private string[] commandLineArgs = [];

    public async Task Run(string[] args)
    {
        this.commandLineArgs = args;
        await ExecuteMode(args.Length > 0 ? args[0] : "NOT_SET");
    }

    private static HttpClient CreateJiraHttpClient()
    {
        var client = new HttpClient();
        var email = Secrets.Username;
        var token = Secrets.JiraToken;
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{token}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        return client;
    }

    private static HttpClient CreateSlackHttpClient()
    {
        var client = new HttpClient();
        var slackToken = Secrets.SlackToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", slackToken);
        return client;
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
            Console.WriteLine($"Task '{mode}' not found.");
            return;
        }

        var sw = Stopwatch.StartNew();
        await selectedTask.ExecuteAsync(this.commandLineArgs);
        sw.Stop();
        Console.WriteLine($"Task '{selectedTask.Key}' completed in {sw.Elapsed.TotalSeconds:N2} seconds.");
    }
}
