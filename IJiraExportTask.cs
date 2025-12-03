namespace BensEngineeringMetrics;

public interface IJiraExportTask
{
    string Description { get; }
    string Key { get; }
    Task ExecuteAsync(string[] args);
}
