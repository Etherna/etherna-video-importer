using Etherna.ServicesClient.Clients.Index;
using Etherna.VideoImporter.Core.Dtos;
using Etherna.VideoImporter.Core.Models;
using Etherna.VideoImporter.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public sealed class CleanerVideoService : ICleanerVideoService
    {
        // Fields.
        private readonly IUserIndexClient ethernaIndexClient;

        // Constractor.
        public CleanerVideoService(
            IUserIndexClient ethernaIndexClient)
        {
            this.ethernaIndexClient = ethernaIndexClient;
        }

        [SuppressMessage("Performance", "CA1851:Possible multiple enumerations of 'IEnumerable' collection", Justification = "Temporary. Remove with next refactoring")]
        public async Task RunCleanerAsync(
            IEnumerable<VideoData> allVideoDataInfos,
            IEnumerable<VideoDto> importedVideos)
        {
            if (allVideoDataInfos == null)
                return;

            Console.WriteLine($"Start cleaner invalid video");

            // Get indexed videos.
            var videoIds = importedVideos.Select(
                    videoData => JsonUtility.FromJson<MetadataPersonalDataDto>(videoData?.LastValidManifest?.PersonalData)?.VideoId)
                .Where(v => !string.IsNullOrWhiteSpace(v));

            // Get video indexed but not in repository files.
            var removableIds = videoIds.Except(allVideoDataInfos.Select(repVideo => repVideo.YoutubeId).ToList());
            foreach (var videoId in removableIds)
            {
                try
                {
                    var itemToRemove = importedVideos
                        .Where(videoData => JsonUtility.FromJson<MetadataPersonalDataDto>(videoData?.LastValidManifest?.PersonalData)?.VideoId == videoId)
                        .First();

                    await ethernaIndexClient.VideosClient.VideosDeleteAsync(itemToRemove.Id).ConfigureAwait(false);

                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    Console.WriteLine($"Video {itemToRemove.Id} removed");
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"Error:{ex.Message} \n Video unable to remove video {videoId}\n");
                    Console.ResetColor();
                }
            }
        }

        public async Task RunOldDeleterAsync(IEnumerable<VideoDto> videos)
        {
            if (videos is null)
                throw new ArgumentNullException(nameof(videos));

            Console.WriteLine($"Start deleter old video");

            foreach (var video in videos)
            {
                if (video.LastValidManifest is not null &&
                    !string.IsNullOrWhiteSpace(video.LastValidManifest.PersonalData))
                    continue;

                try
                {
                    await ethernaIndexClient.VideosClient.VideosDeleteAsync(video.Id).ConfigureAwait(false);
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    Console.WriteLine($"Video {video.Id} removed");
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"Error:{ex.Message} \n Video unable to remove video {video.Id}\n");
                    Console.ResetColor();
                }
            }
        }
    }
}
