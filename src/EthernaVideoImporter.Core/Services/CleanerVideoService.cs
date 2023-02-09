using Etherna.VideoImporter.Core.Dtos;
using Etherna.VideoImporter.Core.Models;
using Etherna.VideoImporter.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public sealed class CleanerVideoService : ICleanerVideoService
    {
        // Fields.
        private readonly EthernaUserClientsAdapter ethernaUserClientsAdapter;
        private readonly string userEthAddr;

        // Constractor.
        public CleanerVideoService(
            EthernaUserClientsAdapter ethernaUserClientsAdapter,
            string userEthAddr)
        {
            this.ethernaUserClientsAdapter = ethernaUserClientsAdapter;
            this.userEthAddr = userEthAddr;
        }

        public async Task RunCleanerAsync(IEnumerable<VideoData> allVideoDataInfos)
        {
            if (allVideoDataInfos == null)
                return;

            Console.WriteLine($"Start cleaner invalid video");
            // Get video indexed
            var importedVideos = await ethernaUserClientsAdapter.GetAllUserVideoAsync(userEthAddr).ConfigureAwait(false);
            var videoIds = importedVideos.Select(
                    videoData => JsonUtility.FromJson<MetadataPersonalDataDto>(videoData?.LastValidManifest?.PersonalData)?.VideoId)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToList();

            // Get video indexed but not in repository files
            var removableIds = videoIds.Except(allVideoDataInfos.Select(repVideo => repVideo.YoutubeId).ToList());
            foreach (var videoId in removableIds)
            {
                try
                {
                    var itemToRemove = importedVideos.Where(
                    videoData => JsonUtility.FromJson<MetadataPersonalDataDto>(videoData?.LastValidManifest?.PersonalData)?.VideoId == videoId)
                        .First();

                    await ethernaUserClientsAdapter.DeleteIndexVideoAsync(itemToRemove.Id).ConfigureAwait(false);

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

        public async Task RunOldDeleterAsync()
        {
            Console.WriteLine($"Start deleter old video");

            var videos = await ethernaUserClientsAdapter.GetAllUserVideoAsync(userEthAddr).ConfigureAwait(false);
            foreach (var video in videos)
            {
                if (video.LastValidManifest is not null &&
                    !string.IsNullOrWhiteSpace(video.LastValidManifest.PersonalData))
                    continue;

                try
                {
                    await ethernaUserClientsAdapter.DeleteIndexVideoAsync(video.Id).ConfigureAwait(false);
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
