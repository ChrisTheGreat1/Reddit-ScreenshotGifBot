using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Configuration;
using Serilog;
using static Google.Apis.Drive.v3.DriveService;

namespace _01152023_Reddit_ScreenshotGifBot.Utility
{
    public class GoogleDriveUtility
    {
        private const string GOOGLE_DRIVE_FOLDER_MIME_TYPE = "application/vnd.google-apps.folder";
        private const string GOOGLE_DRIVE_IMAGE_FILE_MIME_TYPE = "image/jpeg";
        private static readonly ILogger _log = Log.ForContext<Program>();
        private static string GoogleDriveServiceAccountKeysJsonPath { get; set; } = "";
        private static bool IsPropertiesInitialized { get; set; }
        public static async Task<string?> PerformGoogleDriveUpload(string id, List<string> jpgFilePaths)
        {
            if (!IsPropertiesInitialized) throw new Exception("Properties were not properly set before attempting to use Google Drive utility.");

            var gDriveService = GetService(GoogleDriveServiceAccountKeysJsonPath);

            var gDriveFolderId = await CreateFolderAsync(id, gDriveService);

            if (gDriveFolderId == null || gDriveFolderId == "") return null;

            var setPermissionsSuccess = await SetFolderPermissionsAsync(gDriveService, gDriveFolderId);
            var uploadImagesSuccess = await UploadImagesAsync(jpgFilePaths, gDriveService, gDriveFolderId);

            if (setPermissionsSuccess && uploadImagesSuccess) return "https://drive.google.com/drive/folders/" + gDriveFolderId;
            else return null;
        }

        public static void SetPropertiesFromConfig(IConfigurationRoot config)
        {
            GoogleDriveServiceAccountKeysJsonPath = config["googleDriveServiceAccountKeys"];

            IsPropertiesInitialized = true;
        }

        private static async Task<string?> CreateFolderAsync(string id, DriveService gDriveService)
        {
            try
            {
                var gDriveFolder = new Google.Apis.Drive.v3.Data.File();
                gDriveFolder.Name = id;
                gDriveFolder.MimeType = GOOGLE_DRIVE_FOLDER_MIME_TYPE;

                var createGDriveFolderRequest = gDriveService.Files.Create(gDriveFolder);
                var createGDriveFolderResponse = await createGDriveFolderRequest.ExecuteAsync();

                if (createGDriveFolderResponse == null) throw new Exception("Google Drive folder creation request failed.");

                if (createGDriveFolderResponse.Id == null || createGDriveFolderResponse.Id == "")
                    throw new Exception("Google Drive folder was created but Id could not be resolved from API response.");

                return createGDriveFolderResponse.Id;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Exception encountered when attempting to create a new Google Drive folder.");
                return null;
            }
        }

        private static DriveService GetService(string googleDriveServiceAccountKeys)
        {
            try
            {
                var credential = GoogleCredential.FromFile(googleDriveServiceAccountKeys).CreateScoped(ScopeConstants.Drive);

                var service = new DriveService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential
                });

                return service;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Exception encountered when attempting to create the Google Drive service." +
                    "Is the argument string path correctly linked to a .json file listing the Google service account credentials?");

                throw;
            }
        }
        private static async Task<bool> SetFolderPermissionsAsync(DriveService gDriveService, string gDriveFolderId)
        {
            Permission permission = new();
            permission.AllowFileDiscovery = true;
            permission.Type = "anyone";
            permission.Role = "reader";

            try
            {
                var gDriveFolderPermission = gDriveService.Permissions.Create(permission, gDriveFolderId);
                var gDriveFolderPermissionRequest = await gDriveFolderPermission.ExecuteAsync();

                if (gDriveFolderPermissionRequest.Type != "anyone")
                    throw new Exception("Attempt to set permissions for Google Drive folder failed.");

                return true;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Exception encountered when attempting to set the permissions of the Google Drive folder.");
                return false;
            }
        }

        private static void Upload(string jpgFileName, string jpgFilePath, DriveService gDriveService, string gDriveFolderId)
        {
            var gDriveJpg = new Google.Apis.Drive.v3.Data.File();
            gDriveJpg.Name = jpgFileName;
            gDriveJpg.Parents = new string[] { gDriveFolderId };

            using (FileStream stream = System.IO.File.OpenRead(jpgFilePath))
            {
                var gDriveJpgUploadRequest = gDriveService.Files.Create(gDriveJpg, stream, GOOGLE_DRIVE_IMAGE_FILE_MIME_TYPE);

                var gDriveJpgUploadResponse = gDriveJpgUploadRequest.Upload();
            }
        }

        private static async Task<bool> UploadImagesAsync(List<string> jpgFilePaths, DriveService gDriveService, string gDriveFolderId)
        {
            //IList<Task> gDriveUploadTasks = new List<Task>();

            try
            {
                for (int index = 0; index < jpgFilePaths.Count; index++)
                {
                    //gDriveUploadTasks.Add(UploadTaskAsync(index.ToString(), jpgFilePaths[index], gDriveService, gDriveFolderId));

                    Upload(index.ToString(), jpgFilePaths[index], gDriveService, gDriveFolderId);
                }

                //await Task.WhenAll(gDriveUploadTasks);

                return true;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Exception encountered when attempting to upload images to Google Drive.");
                return false;
            }
        }

        private static async Task UploadTaskAsync(string jpgFileName, string jpgFilePath, DriveService gDriveService, string gDriveFolderId)
        {
            var gDriveJpg = new Google.Apis.Drive.v3.Data.File();
            gDriveJpg.Name = jpgFileName;
            gDriveJpg.Parents = new string[] { gDriveFolderId };

            using (FileStream stream = System.IO.File.OpenRead(jpgFilePath))
            {
                var gDriveJpgUploadRequest = gDriveService.Files.Create(gDriveJpg, stream, GOOGLE_DRIVE_IMAGE_FILE_MIME_TYPE);

                var gDriveJpgUploadResponse = await gDriveJpgUploadRequest.UploadAsync();
            }
        }
    }
}