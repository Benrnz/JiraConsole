using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using File = Google.Apis.Drive.v3.Data.File;

namespace BensJiraConsole;

public class GoogleDriveUploader
{
    private static readonly string[] Scopes = { DriveService.Scope.DriveFile };
    private static readonly string ApplicationName = "Google Drive CSV Uploader";

    public async Task UploadCsvAsync(string csvFilePath, string driveFileName, string? folderName = null)
    {
        folderName ??= "BensJiraConsoleUploads";
        UserCredential credential;

        // Load the client secrets from the downloaded JSON file
        await using (var stream = new FileStream("client_secret_apps.googleusercontent.com.json", FileMode.Open, FileAccess.Read))
        {
            credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                (await GoogleClientSecrets.FromStreamAsync(stream)).Secrets,
                Scopes,
                "user",
                CancellationToken.None);
        }

        // Create Drive API service
        using var service = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName
        });

        var parentFolderId = await GetOrCreateFolderIdAsync(service, folderName) ?? string.Empty;

        // Step 1: Search for an existing file with the same name
        var fileId = await FindFileByName(service, driveFileName, parentFolderId);

        // Step 2: Decide whether to create or update
        if (!string.IsNullOrEmpty(fileId))
        {
            // File exists, perform an update
            await UpdateExistingFile(service, fileId, csvFilePath);
            Console.WriteLine($"File '{driveFileName}' updated successfully.");
        }
        else
        {
            // File does not exist, perform a create
            await CreateNewFileInFolderAsync(service, csvFilePath, driveFileName, parentFolderId);
            Console.WriteLine($"File '{driveFileName}' created successfully.");
        }
    }

    private async Task<string?> GetOrCreateFolderIdAsync(DriveService service, string folderName)
    {
        // Search for an existing folder with the specified name
        var listRequest = service.Files.List();
        listRequest.Q = $"name = '{folderName}' and mimeType = 'application/vnd.google-apps.folder'";
        listRequest.Fields = "files(id)";

        var result = await listRequest.ExecuteAsync();
        var folder = result.Files?.FirstOrDefault();

        // If the folder is found, return its ID
        if (folder != null)
        {
            return folder.Id;
        }

        // If the folder doesn't exist, create it under the root directory
        Console.WriteLine($"Folder '{folderName}' not found. Creating a new one...");

        var folderMetadata = new File
        {
            Name = folderName,
            MimeType = "application/vnd.google-apps.folder",
            Parents = new List<string> { "root" } // 'root' is a special alias for the user's root folder
        };

        var request = service.Files.Create(folderMetadata);
        request.Fields = "id"; // Request the new folder's ID
        var newFolder = await request.ExecuteAsync();

        if (newFolder != null)
        {
            Console.WriteLine($"New folder created with ID: {newFolder.Id}");
            return newFolder.Id;
        }

        // Handle the case where folder creation fails
        Console.WriteLine("Error: Failed to create the new folder.");
        return null;
    }

// Helper method to find a file's ID by name
    private async Task<string?> FindFileByName(DriveService service, string fileName, string parentFolderId)
    {
        var listRequest = service.Files.List();

        // The query string to search for the file.
        // 'parentFolderId' is the ID of the folder to search in.
        listRequest.Q = $"name = '{fileName}' and '{parentFolderId}' in parents";

        listRequest.Spaces = "drive";
        listRequest.Fields = "nextPageToken, files(id)";

        var result = await listRequest.ExecuteAsync();

        // Return the ID of the first file found, or null if no file is found.
        return result.Files?.FirstOrDefault()?.Id;
    }

// Helper method to update an existing file
    private async Task UpdateExistingFile(DriveService service, string fileId, string localFilePath)
    {
        await using var stream = new FileStream(localFilePath, FileMode.Open);
        var updateRequest = service.Files.Update(new File(), fileId, stream, "text/csv");
        await updateRequest.UploadAsync();
        var uploadedFile = updateRequest.ResponseBody;
        if (uploadedFile != null)
        {
            Console.WriteLine($"File '{uploadedFile.Name}' uploaded successfully with ID: {uploadedFile.Id}");
        }
    }

    private async Task CreateNewFileInFolderAsync(DriveService service, string localFilePath, string driveFileName, string parentFolderId)
    {
        var fileMetadata = new File
        {
            Name = driveFileName,
            MimeType = "text/csv"
        };

        // If a folder ID was found, add it to the Parents property
        if (!string.IsNullOrEmpty(parentFolderId))
        {
            fileMetadata.Parents = new List<string> { parentFolderId };
        }

        await using var stream = new FileStream(localFilePath, FileMode.Open);
        var request = service.Files.Create(fileMetadata, stream, "text/csv");
        await request.UploadAsync();

        var uploadedFile = request.ResponseBody;
        if (uploadedFile != null)
        {
            Console.WriteLine($"File '{uploadedFile.Name}' uploaded successfully with ID: {uploadedFile.Id}");
        }
    }
}
