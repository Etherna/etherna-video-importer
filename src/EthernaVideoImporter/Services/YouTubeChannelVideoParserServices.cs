using Etherna.EthernaVideoImporter.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Common;

namespace Etherna.EthernaVideoImporter.Services
{
    internal sealed class YouTubeChannelVideoParserServices
    {

        // Methods.
        public static async Task<IEnumerable<VideoDataMinimalInfo>> ToVideoDataMinimalInfoDtosAsync(string channelUrl)
        {
            var youtube = new YoutubeClient();

            var channel = await youtube.Channels.GetByHandleAsync(channelUrl).ConfigureAwait(false);
            var youtubeVideos = await youtube.Channels.GetUploadsAsync(channel.Url);

            List<VideoDataMinimalInfo> videos = new();
            foreach (var video in youtubeVideos)
                videos.Add(new VideoDataMinimalInfo
                {
                    Uri = video.Url,
                });

            return videos;
        }

        public static async Task<VideoData?> ToVideoDataDtosAsync(string uri)
        {
            var youtube = new YoutubeClient();

            var videoInfo = await youtube.Videos.GetAsync(uri).ConfigureAwait(false);

            return new VideoData
            {
                Id = videoInfo.Id,
                YoutubeUrl = videoInfo.Url,
                Title = videoInfo.Title,
                Description = videoInfo.Description
            };
        }
    }
}
