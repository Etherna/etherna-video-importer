using Etherna.EthernaVideoImporterLibrary.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Common;

namespace Etherna.EthernaVideoImporterLibrary.Services
{
    public sealed class YouTubeChannelVideoParserServices : IVideoParseServices
    {

        // Methods.
        public async Task<IEnumerable<VideoDataMinimalInfo>> ToVideoDataMinimalInfoDtosAsync(string uri)
        {
            var youtube = new YoutubeClient();

            var channel = await youtube.Channels.GetByHandleAsync(uri).ConfigureAwait(false);
            var youtubeVideos = await youtube.Channels.GetUploadsAsync(channel.Url);

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
