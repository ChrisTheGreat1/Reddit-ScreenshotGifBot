using _01152023_Reddit_ScreenshotGifBot.Utility;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace _01152023_Reddit_ScreenshotGifBot
{
    public class Program
    {
        public static async Task Main()
        {
            // Access secrets.json file and populate all confidential keys/variables
            var config = new ConfigurationBuilder()
                .AddUserSecrets<Program>()
                .Build();

            // Add Serilog functionality. Hacky solution but this eliminates requirement for incorporating dependency injection just to implement a logger.
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File("logfile.log")
                .CreateLogger();

            ImgurUtility.SetPropertiesFromConfig(config);
            RedditUtility.SetPropertiesFromConfig(config);
            GoogleDriveUtility.SetPropertiesFromConfig(config);

            while (true)
            {
                await RedditUtility.PerformRedditFunctions();
                Thread.Sleep(30000); // Wait 30 seconds before checking Reddit for new messages again.
            }
        }
    }
}