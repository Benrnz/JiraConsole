namespace BensEngineeringMetrics;

public interface ICsvExporter
{
    string Export(IEnumerable<object> issues, Func<string>? overrideSerialiseHeader = null, Func<object, string>? overrideSerialiseRecord = null);
    void SetFileNameMode(FileNameMode mode, string fileNameHint);
}
