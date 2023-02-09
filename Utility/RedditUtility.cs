using _01152023_Reddit_ScreenshotGifBot.Helpers;
using _01152023_Reddit_ScreenshotGifBot.Models;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using RestSharp;
using Serilog;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace _01152023_Reddit_ScreenshotGifBot.Utility
{
    public class RedditUtility
    {
        private const string BASE_URL_OAUTHREDDIT = "https://oauth.reddit.com";
        private const string BASE_URL_REDDIT = "https://www.reddit.com";
        private static readonly ILogger _log = Log.ForContext<Program>();
        private static string? AccessToken { get; set; } = "";
        private static bool IsAccessTokenValid { get; set; } = false;
        private static bool IsPropertiesInitialized { get; set; } = false;
        private static string? RedditAppId { get; set; } = "";
        private static string? RedditAppSecret { get; set; } = "";
        private static string? RefreshToken { get; set; } = "";
        public static async Task PerformRedditFunctions()
        {
            if (!IsPropertiesInitialized) throw new Exception("Reddit user credentials have not been initialized.");

            await GetNewAccessTokenAsync(); // Overkill getting a new access token with each call but this ensures that the token should always be valid.
            var unreadMessages = await CheckForUnreadMessages();

            // If any of these are null, it means there are no unread messages so no further action is required.
            if (unreadMessages == null) return;
            if (unreadMessages.data == null) return;
            if (unreadMessages.data.children == null) return;

            await ProcessMessages(unreadMessages);

            await SetMessagesAsRead(unreadMessages);
        }

        public static void SetPropertiesFromConfig(IConfigurationRoot config)
        {
            RedditAppId = config["redditAppId"];
            RedditAppSecret = config["redditAppSecret"];
            RefreshToken = config["redditRefreshToken"];

            IsPropertiesInitialized = true;
        }
        private static async Task<RedditUnreadMsgResponseJSON?> CheckForUnreadMessages()
        {
            if (!IsAccessTokenValid) throw new Exception("Reddit access token is not valid.");

            var redditUnreadMsgRequest = new RestRequest("/message/unread", Method.Get);
            redditUnreadMsgRequest.AddHeader("Authorization", $"Bearer {AccessToken}");

            var redditUnreadMsgResponse = await RestHttpUtility.ExecuteRequestAsync(BASE_URL_OAUTHREDDIT, redditUnreadMsgRequest);

            if (redditUnreadMsgResponse == null) return null;

            try
            {
                var redditUnreadMsgResponseJSON = JsonConvert.DeserializeObject<RedditUnreadMsgResponseJSON>(redditUnreadMsgResponse.Content);

                if (redditUnreadMsgResponseJSON.data.dist == 0) return null;

                return redditUnreadMsgResponseJSON;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Exception encountered when attempting to check Reddit for unread messages. The HTTP json response could not be mapped to the model.");
                return null;
            }
        }

        private static string CreateDownloadUploadUrlErrorReply()
        {
            return "I experienced issues processing the .gif you have linked to and could not complete your request.";
        }

        private static string CreateJpgConversionErrorReply()
        {
            return "I experienced issues converting your .gif into .jpg images. This likely occurred because the individual jpg images exceed 5 MB in size.";
        }

        private static string CreateNonGifUrlErrorReply(string url)
        {
            return $"I am unable to process the specified URL {url} because it is not a .gif file.";
        }

        private static string CreateReply(string? imgurLink, string? googleDriveLink, string? originalUrl)
        {
            StringBuilder sb = new();

            if (imgurLink != null && imgurLink != "")
            {
                sb.Append("Here is your Imgur album (first 5 images only due to Imgur API limits): ");
                sb.AppendLine($"[Imgur]({imgurLink})");
            }
            else
            {
                sb.AppendLine($"Sorry, I could not successfully upload your images to Imgur.");
            }

            sb.AppendLine();
            sb.AppendLine();

            if (googleDriveLink != null && googleDriveLink != "")
            {
                sb.Append("Here is your Google Drive album: ");
                sb.AppendLine($"[Google Drive]({googleDriveLink})");
            }
            else
            {
                sb.AppendLine($"Sorry, I could not successfully upload your images to Google Drive.");
            }

            sb.AppendLine();
            sb.AppendLine();

            if (originalUrl != null && originalUrl != "")
            {
                sb.Append("Original GIF: ");
                sb.Append($"[GIF]({originalUrl})");
            }
            else
            {
                sb.AppendLine($"Sorry, I could not find the original GIF url.");
            }

            return sb.ToString();
        }

        private static string CreateUnknownUrlErrorReply()
        {
            return "I could not find a URL for a .gif. Please ensure that there is a valid URL directly in your comment mentioning me, " +
                "in the comment that you are responding to, or in the post that you are commenting on.";
        }

        private static async Task<string?> DetermineUrl(RedditUnreadMsgResponseJSON.Child message)
        {
            string? url = ExtractUrl(message.data.body_html);

            if (url == null)
            {
                var parentId = message.data.parent_id;

                if (parentId == null || parentId == "") url = null;

                if (parentId.StartsWith("t1")) url = await ExtractUrlFromParentComment(message, parentId);
                else if (parentId.StartsWith("t3")) url = await ExtractUrlFromPost(parentId);
                else url = null;
            }

            return url;
        }

        private static string? ExtractUrl(string? messageBodyHtml)
        {
            if (messageBodyHtml == null || messageBodyHtml == "") return null;

            try
            {
                var messageBodyHtmlDecoded = WebUtility.HtmlDecode(messageBodyHtml);

                string urlRegex = @"[(http(s)?):\/\/(www\.)?a-zA-Z0-9@:%._\+~#=]{2,256}\.[a-z]{2,6}\b([-a-zA-Z0-9@:%_\+.~#?&//=]*)";
                Regex regex = new Regex(urlRegex);

                MatchCollection urlRegexMatches = regex.Matches(messageBodyHtmlDecoded);

                List<string> urls = new();

                foreach (var match in urlRegexMatches)
                {
                    if (match == null) continue;

                    if (!Uri.IsWellFormedUriString(match.ToString(), UriKind.Absolute)) continue;

                    urls.Add(match.ToString());
                }

                urls = urls.Distinct().ToList();

                if (urls.Count == 0) return null;

                return urls.First();
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Exception encountered when attempting to perform Regex url extraction.");
                return null;
            }
        }

        private static async Task<string?> ExtractUrlFromParentComment(RedditUnreadMsgResponseJSON.Child message, string parentId)
        {
            string? url = null;
            string? resourceUrl = "";

            try
            {
                // Get the name of the parent comment, then use the Reddit "api" of appending ".json" to the URL of the comment context to retrieve it.

                var parentIdSuffix = parentId.Substring(3);

                var contextIndex = message.data.context.IndexOf(message.data.id);
                resourceUrl = message.data.context.Substring(0, contextIndex - 1) + $"/{parentIdSuffix}" + ".json";
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Exception encountered when attempting to extract URL from Reddit parent comment. The parent comment ID could not be resolved.");
                return null;
            }

            var redditCommentRequest = new RestRequest(resourceUrl, Method.Get);

            var redditCommentResponse = await RestHttpUtility.ExecuteRequestAsync(BASE_URL_REDDIT, redditCommentRequest);

            if (redditCommentResponse == null) return null;

            try
            {
                RedditGetCommentByContextIdResponseJSON[]? redditCommentResponseJSON = JsonConvert.DeserializeObject<RedditGetCommentByContextIdResponseJSON[]>(redditCommentResponse.Content);

                var comment = redditCommentResponseJSON.Last();

                var children = comment.data.children;

                foreach (var child in children)
                {
                    if (child.kind != "t1") continue;

                    if (child.data.name == parentId)
                    {
                        url = ExtractUrl(child.data.body_html);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Exception encountered when attempting to extract URL from Reddit parent comment. The HTTP json response could not be mapped to the model.");
                return null;
            }

            return url;
        }

        private static async Task<string?> ExtractUrlFromPost(string parentId)
        {
            string? url = null;

            var redditGetPostByIdRequest = new RestRequest($"/by_id/{parentId}", Method.Get);
            redditGetPostByIdRequest.AddHeader("Authorization", $"Bearer {AccessToken}");

            var redditGetPostByIdResponse = await RestHttpUtility.ExecuteRequestAsync(BASE_URL_OAUTHREDDIT, redditGetPostByIdRequest);

            if (redditGetPostByIdResponse == null) return null;

            try
            {
                var redditGetPostByIdResponseJSON = JsonConvert.DeserializeObject<RedditGetPostByIdResponseJSON>(redditGetPostByIdResponse.Content);

                var children = redditGetPostByIdResponseJSON.data.children;

                foreach (var child in children)
                {
                    if (child.kind != "t3") continue;

                    if (child.data.name == parentId)
                    {
                        if (child.data.is_self)
                        {
                            url = ExtractUrl(child.data.selftext_html);
                        }
                        else
                        {
                            if (child.data.url != null && child.data.url != "")
                                url = child.data.url;
                        }

                        break;
                    }
                }

                return url;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Exception encountered when attempting to extract URL from Reddit post. The HTTP json response could not be mapped to the model.");
                return null;
            }
        }

        private static async Task GetNewAccessTokenAsync()
        {
            if (!IsPropertiesInitialized) throw new Exception("Reddit secret properties were not properly initialized.");

            string appIdSecretBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(RedditAppId + ":" + RedditAppSecret));

            var redditAccessTokenRequest = new RestRequest("/api/v1/access_token", Method.Post);

            redditAccessTokenRequest.AddHeader("Authorization", $"Basic {appIdSecretBase64}");
            redditAccessTokenRequest.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            redditAccessTokenRequest.AddParameter("grant_type", "refresh_token");
            redditAccessTokenRequest.AddParameter("refresh_token", RefreshToken);

            var redditAccessTokenResponse = await RestHttpUtility.ExecuteRequestAsync(BASE_URL_REDDIT, redditAccessTokenRequest);

            if (redditAccessTokenResponse == null) throw new Exception("Request for a new Reddit OAuth access token failed.");

            try
            {
                var redditAccessTokenResponseJSON = JsonConvert.DeserializeObject<RedditRefreshTokenResponseJSON>(redditAccessTokenResponse.Content);
                var accessToken = redditAccessTokenResponseJSON.access_token;

                if (accessToken == null || accessToken == "")
                    throw new NullReferenceException("Reddit responded with a successful JSON response but the new access token could not be resolved/parsed to the model object.");

                AccessToken = accessToken;
                IsAccessTokenValid = true;
            }
            catch (Exception ex)
            {
                IsAccessTokenValid = false;
                _log.Error(ex, "Exception encountered when attempting to parse the new Reddit access token. The HTTP json response could not be mapped to the model.");
                return;
            }
        }

        private static async Task ProcessMessages(RedditUnreadMsgResponseJSON unreadMessages)
        {
            foreach (var message in unreadMessages.data.children)
            {
                if (message.data.type != "username_mention") continue;

                string messageName = message.data.name;

                if (messageName == null || messageName == "") continue;

                var url = await DetermineUrl(message);

                if (url == null || url == "")
                {
                    await Reply(messageName, CreateUnknownUrlErrorReply());
                    continue;
                }

                string workingDirectory = DirectoryHelpers.CreateWorkingDirectory(messageName);

                var gifPath = await GifUtility.DownloadGifAsync(url, workingDirectory);

                if (gifPath == null || gifPath == "")
                {
                    await Reply(messageName, CreateNonGifUrlErrorReply(url));
                    continue;
                }

                var jpgFilePaths = GifUtility.SplitAndSaveGifIntoJpg(gifPath, workingDirectory);

                if (jpgFilePaths == null)
                {
                    await Reply(messageName, CreateJpgConversionErrorReply());
                    continue;
                }

                var imgurLink = await ImgurUtility.PerformImgurUpload(jpgFilePaths);

                var googleDriveLink = await GoogleDriveUtility.PerformGoogleDriveUpload(messageName, jpgFilePaths);

                string replyText = CreateReply(imgurLink, googleDriveLink, url);

                await Reply(messageName, replyText);

                Directory.Delete(workingDirectory, true);
            }
        }
        private static async Task Reply(string thing_id, string replyText)
        {
            if (thing_id == "" || thing_id == null)
            {
                _log.Warning("A null or empty string was passed in for the thing_id argument.");
                return;
            }

            var redditReplyRequest = new RestRequest("/api/comment", Method.Post);
            redditReplyRequest.AddHeader("Authorization", $"Bearer {AccessToken}");
            redditReplyRequest.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            redditReplyRequest.AddParameter("thing_id", thing_id);
            redditReplyRequest.AddParameter("text", replyText);

            await RestHttpUtility.ExecuteRequestAsync(BASE_URL_OAUTHREDDIT, redditReplyRequest);
        }
        private static async Task SetMessagesAsRead(RedditUnreadMsgResponseJSON messages)
        {
            if (!IsAccessTokenValid) throw new Exception("Reddit access token is not valid.");

            List<string> messageNames = new();

            foreach (var msg in messages.data.children)
            {
                messageNames.Add(msg.data.name);
            }

            if (messageNames.Count == 0) return;

            StringBuilder sb = new();
            var messageIds = sb.AppendJoin(',', messageNames.ToArray()).ToString();

            var redditSetMsgReadRequest = new RestRequest("/api/read_message", Method.Post);
            redditSetMsgReadRequest.AddHeader("Authorization", $"Bearer {AccessToken}");
            redditSetMsgReadRequest.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            redditSetMsgReadRequest.AddParameter("id", messageIds);

            await RestHttpUtility.ExecuteRequestAsync(BASE_URL_OAUTHREDDIT, redditSetMsgReadRequest);
        }
    }
}