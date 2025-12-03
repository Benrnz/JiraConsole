namespace BensEngineeringMetrics;

public interface ICloudUploader
{
    Task UploadCsvAsync(string csvFilePath, string cloudFileName, string? folderName = null);
}
