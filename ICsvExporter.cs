namespace BensJiraConsole;

public interface ICsvExporter
{
    FileNameMode Mode { get; set; }
    Func<string>? OverrideSerialiseHeader { get; set; }
    Func<object, string>? OverrideSerialiseRecord { get; set; }
    string Export(IEnumerable<object> issues, string? fileNameHint = null);
}
