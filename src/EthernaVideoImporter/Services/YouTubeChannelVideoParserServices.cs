using Etherna.VideoImporter.Core.Models;
using Etherna.VideoImporter.Core.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Common;

namespace Etherna.VideoImporter.Services
{
    public sealed class YouTubeChannelVideoParserServices : IVideoParseServices
    {
        // Methods.
        public async Task<IEnumerable<VideoDataMinimalInfo>> ToVideoDataMinimalInfoDtosAsync(string uri)
        {
            var youtubeClient = new YoutubeClient();

            var channel = await youtubeClient.Channels.GetByHandleAsync(uri).ConfigureAwait(false);
            var youtubeVideos = await youtubeClient.Channels.GetUploadsAsync(channel.Url);

            List<VideoDataMinimalInfo> videos = new();
            foreach (var video in youtubeVideos)
                videos.Add(new VideoDataMinimalInfo
                {
                    Uri = video.Url,
                });

            return videos;
        }

        public async Task<VideoData?> ToVideoDataDtosAsync(string uri)
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
