using Serilog;

namespace _01152023_Reddit_ScreenshotGifBot.Helpers
{
    public class DirectoryHelpers
    {
        private static readonly ILogger _log = Log.ForContext<Program>();

        public static string CreateWorkingDirectory(string directoryName)
        {
            try
            {
                string workingDirectory = Directory.GetCurrentDirectory() + "\\" + directoryName;

                if (Directory.Exists(workingDirectory))
                {
                    Directory.Delete(workingDirectory, true);
                }

                Directory.CreateDirectory(workingDirectory);

                return workingDirectory;
            }
            catch (Exception ex)
            {
                _log.Fatal(ex, "Exception encountered when attempting to create a new directory.");
                throw;
            }
        }
    }
}