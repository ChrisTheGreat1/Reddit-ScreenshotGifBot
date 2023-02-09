namespace _01152023_Reddit_ScreenshotGifBot.Models
{
    public class RedditGetPostByIdResponseJSON
    {
        public string? kind { get; set; }
        public Data? data { get; set; }

        public class Data
        {
            public Child[]? children { get; set; }
        }

        public class Child
        {
            public string? kind { get; set; }
            public Data1? data { get; set; }
        }

        public class Data1
        {
            public string? selftext { get; set; }
            public string? name { get; set; }
            public bool is_self { get; set; }
            public string? selftext_html { get; set; }
            public string? id { get; set; }
            public string? url { get; set; }
        }
    }
}