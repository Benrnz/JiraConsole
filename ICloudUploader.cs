namespace BensJiraConsole;

public interface ICloudUploader
{
    Task UploadCsvAsync(string csvFilePath, string cloudFileName, string? folderName = null);
}
