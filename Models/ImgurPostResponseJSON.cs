namespace _01152023_Reddit_ScreenshotGifBot.Models
{
    public class ImgurPostResponseJSON
    {
        public Data? data { get; set; }
        public bool success { get; set; }
        public int status { get; set; }

        public class Data
        {
            public string id { get; set; } = "";
            public string deletehash { get; set; } = "";
        }
    }
}