using _01152023_Reddit_ScreenshotGifBot.Models;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using RestSharp;
using Serilog;

namespace _01152023_Reddit_ScreenshotGifBot.Utility
{
    public class ImgurUtility
    {
        private const string BASE_URL_IMGUR = "https://api.imgur.com/3/";
        private const int MAX_NUM_IMGUR_UPLOADS = 5;
        private static readonly ILogger _log = Log.ForContext<Program>();
        private static string ImgurClientId { get; set; } = "";
        private static bool IsPropertiesInitialized { get; set; } = false;
        public static async Task<string?> PerformImgurUpload(List<string> jpgFilePaths)
        {
            if (!IsPropertiesInitialized) throw new Exception("Properties were not properly set before attempting to use Imgur utility.");

            // Need to upload images first, otherwise Imgur API fails randomly when attempting to upload images directly into an album.
            // Too many hours spent trying to troubleshoot method of creating the album first then uploading images directly into album.
            var imgurDeleteHashes = await UploadImagesAsync(jpgFilePaths);

            if (imgurDeleteHashes == null) return null;

            var imgurAlbumId = await CreateAlbumAsync(imgurDeleteHashes);

            if (imgurAlbumId == null || imgurAlbumId == "") return null;

            return "https://imgur.com/a/" + imgurAlbumId;
        }

        public static void SetPropertiesFromConfig(IConfigurationRoot config)
        {
            ImgurClientId = config["imgurClientId"];

            IsPropertiesInitialized = true;
        }
        private static async Task<string?> CreateAlbumAsync(List<string> imgurDeleteHashes)
        {
            var imgurAlbumCreationRequest = new RestRequest("album", Method.Post);
            imgurAlbumCreationRequest.AddHeader("Authorization", $"Client-ID {ImgurClientId}");

            foreach (var deleteHash in imgurDeleteHashes)
            {
                imgurAlbumCreationRequest.AddParameter("deletehashes[]", deleteHash); // Add each deletehash as a seperate parameter.
            }

            var imgurAlbumCreationResponse = await RestHttpUtility.ExecuteRequestAsync(BASE_URL_IMGUR, imgurAlbumCreationRequest);

            if (imgurAlbumCreationResponse == null) return null;

            try
            {
                var imgurAlbumCreationResponseJSON = JsonConvert.DeserializeObject<ImgurPostResponseJSON>(imgurAlbumCreationResponse.Content);
                var imgurAlbumId = imgurAlbumCreationResponseJSON.data.id;

                if (imgurAlbumId == null || imgurAlbumId == "") throw new Exception("Imgur album ID could not be resolved/parsed from the JSON response.");
                else return imgurAlbumId;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Exception encountered when attempting to create a new Imgur album.");
                return null;
            }
        }

        private static async Task<List<string>?> UploadImagesAsync(List<string> jpgFilePaths)
        {
            List<string>? imgurDeleteHashes = new();
            IList<Task<string?>> imgurUploadTasks = new List<Task<string?>>();

            try
            {
                using (var imgurUploadClient = new RestClient(BASE_URL_IMGUR))
                {
                    int numUploads = 0;

                    foreach (var path in jpgFilePaths)
                    {
                        // Due to Imgur upload limits, limit the number of Imgur image uploads. Use this break clause instead of a for-loop
                        // to account for scenario where 5 or less images were extracted from .gif
                        if (numUploads >= MAX_NUM_IMGUR_UPLOADS) break;

                        imgurUploadTasks.Add(UploadTaskAsync(ImgurClientId, path, imgurUploadClient));

                        numUploads++;
                    }

                    await Task.WhenAll(imgurUploadTasks);
                }

                foreach (var task in imgurUploadTasks)
                {
                    if (!task.IsCompletedSuccessfully) continue;

                    if (task.Result == null || task.Result == "") continue;

                    imgurDeleteHashes.Add(task.Result);
                }

                if (imgurDeleteHashes.Count == 0) return null;

                return imgurDeleteHashes;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Exception encountered when attempting to upload images to Imgur");
                return null;
            }
        }
        private static async Task<string?> UploadTaskAsync(string imgurClientId, string jpgFilePath, RestClient imgurUploadClient)
        {
            try
            {
                var imgurJpgUploadRequest = new RestRequest("upload", Method.Post);
                imgurJpgUploadRequest.AddHeader("Authorization", $"Client-ID {imgurClientId}");
                imgurJpgUploadRequest.AddFile("image", jpgFilePath);

                var imgurJpgUploadResponse = await imgurUploadClient.ExecuteAsync(imgurJpgUploadRequest);

                if (!imgurJpgUploadResponse.IsSuccessStatusCode || !imgurJpgUploadResponse.IsSuccessful) throw new Exception($"Imgur image upload task failed: {imgurJpgUploadResponse.Content}");

                var imgurJpgUploadResponseJSON = JsonConvert.DeserializeObject<ImgurPostResponseJSON>(imgurJpgUploadResponse.Content);
                var deleteHash = imgurJpgUploadResponseJSON.data.deletehash;

                if (deleteHash == null || deleteHash == "") throw new Exception("Attempt to parse deletehash from Imgur JSON response failed.");
                else return deleteHash;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Exception encountered when attempting to upload an image to Imgur.");
                return null;
            }
        }
    }
}