using System.Net;

namespace YoutubeDownloader.Handlers
{
    class YoutubeHandler
    {
        public static HttpMessageHandler GetHandler()
        {
            CookieContainer cookieContainer = new();
            cookieContainer.Add(new Cookie("CONSENT", "YES+cb", "/", "youtube.com"));
            return new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = cookieContainer
            };

        }
    }
}
