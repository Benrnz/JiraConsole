namespace BensEngineeringMetrics;

public interface IEngineeringMetricsTask
{
    string Description { get; }
    string Key { get; }
    Task ExecuteAsync(string[] args);
}
