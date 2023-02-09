using RestSharp;
using Serilog;

namespace _01152023_Reddit_ScreenshotGifBot.Utility
{
    public class RestHttpUtility
    {
        private static readonly ILogger _log = Log.ForContext<Program>();

        public static async Task<RestResponse?> ExecuteRequestAsync(string baseUrl, RestRequest request)
        {
            try
            {
                using (var client = new RestClient(baseUrl))
                {
                    var response = await client.ExecuteAsync(request);

                    if (response.IsSuccessful && response.IsSuccessStatusCode) return response;
                    else throw new Exception("Http request via RestHttpUtility class returned a non-successful error code.");
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Exception encountered when performing HTTP request.");
                return null;
            }
        }
    }
}