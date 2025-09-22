using System.Reflection;
using BensJiraConsole;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public static class Program
{
    private static string[] CommandLineArgs = [];

    public static async Task Main(string[] args)
    {
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<App>();
            services.AddTransient<ICsvExporter, SimpleCsvExporter>();
            services.AddTransient<IJiraQueryRunner, JiraQueryDynamicRunner>();
            services.AddTransient<ICloudUploader, GoogleDriveUploader>();
            services.AddTransient<IWorkSheetUpdater, GoogleSheetUpdater>();
            services.AddTransient<IWorkSheetReader, GoogleSheetReader>();

            // Find and Register all tasks
            foreach (var taskType in TaskTypes())
            {
                services.AddSingleton(typeof(IJiraExportTask), taskType);
                services.AddSingleton(taskType);
            }
        });

        var host = builder.Build();
        var app = host.Services.GetRequiredService<App>();

        Console.WriteLine("Jira Console Exporter tool.  Select a task to execute, or 'exit' to quit.");
        await app.Run(args);
        Console.WriteLine("Exiting.");
    }

    private static IEnumerable<Type> TaskTypes()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetTypes().Where(type => typeof(IJiraExportTask).IsAssignableFrom(type) && type is { IsInterface: false, IsAbstract: false });
    }
}
