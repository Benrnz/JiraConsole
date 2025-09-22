using BensJiraConsole;

public static class Program
{
    private static string[] CommandLineArgs = [];

    public static async Task Main(string[] args)
    {
        var app = App.Configure();

        Console.WriteLine("Jira Console Exporter tool.  Select a task to execute, or 'exit' to quit.");
        await app.Run(args);

        Console.WriteLine("Exiting.");
    }
}
