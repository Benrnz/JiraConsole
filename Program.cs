using System.Reflection;
using BensJiraConsole;
using BensJiraConsole.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<App>();
            services.AddTransient<ICsvExporter, SimpleCsvExporter>();
            services.AddTransient<IJiraQueryRunner, JiraQueryDynamicRunner>();
            services.AddTransient<IGreenHopperRunner, GreenHopperDynamicRunner>();
            services.AddTransient<ICloudUploader, GoogleDriveUploader>();
            services.AddTransient<IWorkSheetUpdater, GoogleSheetUpdater>();
            services.AddTransient<IWorkSheetReader, GoogleSheetReader>();
            services.AddSingleton<BugStatsWorker>();

            // Find and Register all tasks
            foreach (var taskType in TaskTypes())
            {
                services.AddSingleton(taskType);
                services.AddSingleton<IJiraExportTask>(sp => (IJiraExportTask)sp.GetRequiredService(taskType));
            }
        });

        var host = builder.Build();
        var app = host.Services.GetRequiredService<App>();

        if (!args.Any())
        {
            // If no arguments passed then its running in user-interactive mode.
            Console.WriteLine("Jira Console Exporter tool.  Select a task to execute, or 'exit' to quit.");
        }

        await app.Run(args);

        if (!args.Any())
        {
            Console.WriteLine("Exiting.");
        }
    }

    private static IEnumerable<Type> TaskTypes()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetTypes().Where(type => typeof(IJiraExportTask).IsAssignableFrom(type) && type is { IsInterface: false, IsAbstract: false });
    }
}
