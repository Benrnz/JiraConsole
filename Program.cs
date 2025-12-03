using System.Reflection;
using BensEngineeringMetrics.Jira;
using BensEngineeringMetrics.Slack;
using BensEngineeringMetrics.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BensEngineeringMetrics;
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
            services.AddTransient<IGreenHopperClient, JiraGreenHopperClient>();
            services.AddTransient<ICloudUploader, GoogleDriveUploader>();
            services.AddTransient<IWorkSheetUpdater, GoogleSheetUpdater>();
            services.AddTransient<IWorkSheetReader, GoogleSheetReader>();
            services.AddSingleton<BugStatsWorker>();
            services.AddTransient<ISlackClient, SlackClient>();

            // Find and Register all tasks
            foreach (var taskType in TaskTypes())
            {
                services.AddSingleton(taskType);
                services.AddSingleton<IEngineeringMetricsTask>(sp => (IEngineeringMetricsTask)sp.GetRequiredService(taskType));
            }
        });

        var host = builder.Build();
        var app = host.Services.GetRequiredService<App>();

        await app.Run(args);
    }

    private static IEnumerable<Type> TaskTypes()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetTypes().Where(type => typeof(IEngineeringMetricsTask).IsAssignableFrom(type) && type is { IsInterface: false, IsAbstract: false });
    }
}
