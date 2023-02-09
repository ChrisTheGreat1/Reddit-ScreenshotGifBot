using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;

namespace _01152023_Reddit_ScreenshotGifBot.Utility
{
    public class GifUtility
    {
        private const long MAX_GIF_SIZE_BYTES = 20000000;

        private const long MAX_JPG_SIZE_BYTES = 5000000;

        private static readonly ILogger _log = Log.ForContext<Program>();

        public static async Task<string?> DownloadGifAsync(string gifUrl, string workingDirectory)
        {
            try
            {
                string gifPath = workingDirectory + "\\downloadedGif.gif";
                HttpResponseMessage? gifDownloadResponse = new();

                using (var gifDownloadClient = new HttpClient())
                {
                    gifDownloadResponse = await gifDownloadClient.GetAsync(gifUrl);
                }

                if (!gifDownloadResponse.IsSuccessStatusCode
                    || gifDownloadResponse.Content.Headers.ContentLength == null
                    || gifDownloadResponse.Content.Headers.ContentType == null) return null;

                var responseContentLength = gifDownloadResponse.Content.Headers.ContentLength;
                var responseContentType = gifDownloadResponse.Content.Headers.ContentType.ToString().ToLower();

                if (responseContentLength < MAX_GIF_SIZE_BYTES && responseContentType == "image/gif")
                {
                    var gifByteArray = await gifDownloadResponse.Content.ReadAsByteArrayAsync();
                    await System.IO.File.WriteAllBytesAsync(gifPath, gifByteArray);
                    return gifPath;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                _log.Fatal(ex, "Exception encountered when attempting to download .gif.");
                throw;
            }
        }

        public static List<string>? SplitAndSaveGifIntoJpg(string gifPath, string workingDirectory)
        {
            List<string> jpgFilePaths = new();

            try
            {
                using (FileStream stream = File.OpenRead(gifPath))
                {
                    GifDecoder gifDecoder = new();
                    gifDecoder.DecodingMode = SixLabors.ImageSharp.Metadata.FrameDecodingMode.All;
                    using var decodedGif = gifDecoder.Decode(Configuration.Default, stream, new CancellationToken());

                    int count = 0;
                    string jpgLocalPath = "";

                    for (int i = 0; i < decodedGif.Frames.Count; i++)
                    {
                        jpgLocalPath = workingDirectory + $"\\{i}.jpg";

                        using var image = decodedGif.Frames.CloneFrame(i);

                        image.SaveAsJpeg(jpgLocalPath);

                        FileInfo fileInfo = new FileInfo(jpgLocalPath);

                        if (fileInfo.Length < MAX_JPG_SIZE_BYTES)
                        {
                            jpgFilePaths.Add(jpgLocalPath);
                            count++;
                        }
                        else _log.Warning("Extracted jpg image exceeded 5 MB in size and was skipped.");
                    }
                }

                if (jpgFilePaths.Count == 0) return null;

                return jpgFilePaths;
            }
            catch (Exception ex)
            {
                _log.Fatal(ex, "Exception encountered when attempting to decode .gif and save individual .jpg images.");
                throw;
            }
        }
    }
}