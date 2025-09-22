namespace BensJiraConsole;

public interface ICsvExporter
{
    void SetFileNameMode(FileNameMode mode, string fileNameHint);

    string Export(IEnumerable<object> issues, Func<string>? overrideSerialiseHeader = null, Func<object, string>? overrideSerialiseRecord = null);
}
