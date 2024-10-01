// Copyright 2022-present Etherna SA
// This file is part of Etherna Video Importer.
// 
// Etherna Video Importer is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// Etherna Video Importer is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with Etherna Video Importer.
// If not, see <https://www.gnu.org/licenses/>.

using Etherna.VideoImporter.Core.Services;
using Etherna.VideoImporter.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using YoutubeExplode.Common;
using YoutubeExplode.Exceptions;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public abstract class YouTubeVideoMetadataBase(
        IYoutubeDownloader youtubeDownloader,
        string youtubeUrl,
        string? playlistName,
        IVideoProvider videoProvider)
        : VideoMetadataBase(videoProvider)
    {
        // Consts.
        private readonly TimeSpan FetchRetryDelay = TimeSpan.FromMinutes(10);
        private const int FetchRetryMax = 20;

        // Properties.
        public string? ChannelName { get; private set; }
        public string? PlaylistName { get; private set; } = playlistName;
        public override IEnumerable<string> SourceOldIds => Array.Empty<string>();
        public Thumbnail? Thumbnail { get; protected set; }
        public string YoutubeId
        {
            get
            {
                var uri = new Uri(YoutubeUrl);
                var query = HttpUtility.ParseQueryString(uri.Query);

                if (query.AllKeys.Contains("v"))
                    return query["v"]!;

                return uri.Segments.Last();
            }
        }
        public string YoutubeUrl { get; } = youtubeUrl;

        // Protected properties.
        protected IYoutubeDownloader YoutubeDownloader { get; } = youtubeDownloader;

        // Methods.
        public override async Task<bool> TryFetchMetadataAsync(
            IIoService ioService)
        {
            ArgumentNullException.ThrowIfNull(ioService, nameof(ioService));
            
            /*
             * YouTube could block fetches to avoid data scrapping.
             * If this happens, we need to retry with enough delay.
             */
            for (int i = 0; i < FetchRetryMax; i++)
            {
                try
                {
                    var metadata = await YoutubeDownloader.YoutubeClient.Videos.GetAsync(YoutubeUrl);
                    ChannelName = metadata.Author.ChannelTitle;
                    Description = metadata.Description;
                    Duration = metadata.Duration ??
                               throw new InvalidOperationException("Live streams are not supported");
                    Thumbnail = metadata.Thumbnails.MaxBy(t => t.Resolution.Area);
                    Title = metadata.Title;

                    ioService.WriteLine($"Fetched YouTube metadata for {metadata.Title}");

                    return true;
                }
                catch (HttpRequestException ex)
                {
                    ioService.WriteErrorLine($"Error retrieving video: {YoutubeUrl}. Try again later");
                    ioService.PrintException(ex);
                }
                catch (TimeoutException ex)
                {
                    ioService.WriteErrorLine($"Time out retrieving video: {YoutubeUrl}. Try again later");
                    ioService.PrintException(ex);
                }
                catch (VideoUnavailableException ex)
                {
                    // Skip because video has been obscured.
                    ioService.WriteErrorLine($"Unavailable video: {YoutubeUrl}. Skipped");
                    ioService.PrintException(ex);
                    return false;
                }
                catch (VideoUnplayableException ex)
                {
                    ioService.WriteErrorLine($"Unplayable video: {YoutubeUrl}");
                    ioService.PrintException(ex);
                }
                catch (YoutubeExplodeException ex)
                {
                    ioService.WriteErrorLine($"Can't read information from YouTube: {YoutubeUrl}");
                    ioService.PrintException(ex);
                }

                if (i + 1 < FetchRetryMax)
                {
                    ioService.WriteLine($"Retry in {FetchRetryDelay.TotalMinutes} minutes");
                    await Task.Delay(FetchRetryDelay);
                }
            }

            return false;
        }
    }
}
