﻿using Etherna.EthernaVideoImporter.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Converter;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace Etherna.EthernaVideoImporter.Services
{
    public sealed class VideoDownloaderService : IVideoDownloaderService, IDisposable
    {
        // Const.
        private const int MAX_RETRY = 3;

        // Fields.
        private readonly string ffMpegFolderPath;
        private readonly string tmpFolder;
        private readonly HttpClient client = new();
        private readonly YoutubeClient youTubeClient = new();

        // Constractor.
        public VideoDownloaderService(
            string ffMpegFolderPath,
            string tmpFolder)
        {
            this.ffMpegFolderPath = ffMpegFolderPath;
            this.tmpFolder = tmpFolder;
        }

        // Public methods.
        public async Task<VideoData> StartDownloadAsync(VideoData videoData)
        {
            if (string.IsNullOrWhiteSpace(videoData?.YoutubeUrl))
                throw new InvalidOperationException("Invalid YoutubeUrl");

            try
            {
                // Take best video resolution.
                var videoResolutions = await DownloadAllResolutionAsync(videoData).ConfigureAwait(false);
                if (!videoResolutions.Any())
                    throw new InvalidOperationException($"Not found video");

                videoData.SetVideoResolutions(videoResolutions);

                return videoData;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void Dispose()
        {
            client?.Dispose();
        }

        // Private Methods.
        private async Task<List<VideoDataResolution>> DownloadAllResolutionAsync(VideoData videoData)
        {
            if (videoData is null)
                throw new ArgumentNullException(nameof(videoData));
            if (string.IsNullOrWhiteSpace(videoData.YoutubeUrl))
                throw new InvalidOperationException("Invalid youtube url");

            // Get manifest data
            var videoManifest = await youTubeClient.Videos.GetAsync(videoData.YoutubeUrl).ConfigureAwait(false);
            var streamManifest = await youTubeClient.Videos.Streams.GetManifestAsync(videoData.YoutubeUrl).ConfigureAwait(false);
            var streamInfos = streamManifest.GetMuxedStreams();

            // Get filename from video title
            var videoTitleBuilder = new StringBuilder(videoManifest.Title);
            foreach (char c in Path.GetInvalidFileNameChars())
                videoTitleBuilder = videoTitleBuilder.Replace(c, '_');
            var videoTitle = videoTitleBuilder.ToString();

            var resolutionVideoQuality = new List<string>();
            var sourceVideoInfos = new List<VideoDataResolution>();
            // Take muxed streams.
            var allResolutions = streamInfos
                .Where(stream => stream.Container == Container.Mp4)
                .OrderBy(res => res.VideoResolution.Area)
                .Distinct();
            foreach (var currentRes in allResolutions)
            {
                resolutionVideoQuality.Add(currentRes.VideoQuality.Label);

                var videoDataResolution = await DownloadVideoAsync(
                    currentRes,
                    null,
                    videoTitle,
                    videoManifest).ConfigureAwait(false);
                sourceVideoInfos.Add(videoDataResolution);
            }

            if (ExistFFmpeg())
            {
                // Take highest quality MP4 video-only stream and highest bitrate audio-only stream
                var streamInfo = streamManifest
                    .GetVideoOnlyStreams()
                    .Where(stream => stream.Container == Container.Mp4)
                    .OrderBy(stream => stream.VideoResolution.Area);
                var bestStreamAudioInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
                foreach (var currentRes in streamInfo)
                {
                    if (resolutionVideoQuality.Contains(currentRes.VideoQuality.Label) ||
                        bestStreamAudioInfo == null)
                        continue;

                    resolutionVideoQuality.Add(currentRes.VideoQuality.Label);

                    var videoDataResolution = await DownloadVideoAsync(
                        currentRes,
                        bestStreamAudioInfo,
                        videoTitle,
                        videoManifest).ConfigureAwait(false);
                    sourceVideoInfos.Add(videoDataResolution);
                }
            }

            return sourceVideoInfos;
        }

        private async Task<VideoDataResolution> DownloadVideoAsync(
            IVideoStreamInfo videoStreamInfo,
            IStreamInfo? audioStreamForMuxInfo,
            string videoTitle,
            Video videoManifest)
        {
            var videoName = $"{videoTitle}_{videoStreamInfo.VideoResolution}";
            var filename = audioStreamForMuxInfo is null ? $"{videoName}.{videoStreamInfo.Container}" : $"{videoName}.muxed.{videoStreamInfo.Container}";
            var videoDataResolution = new VideoDataResolution(
                videoStreamInfo.Bitrate.BitsPerSecond,
                Path.Combine(tmpFolder, filename),
                videoName,
                videoStreamInfo.VideoQuality.Label);
            if (videoManifest.Duration is null)
                throw new InvalidOperationException("Invalid duration video");

            var i = 0;
            var downloaded = false;
            while (i < MAX_RETRY &&
                    !downloaded)
                try
                {
                    i++;

                    // Download and process them into one file
                    if (audioStreamForMuxInfo is null)
                        await youTubeClient.Videos.Streams.DownloadAsync(
                        videoStreamInfo,
                        videoDataResolution.DownloadedFilePath,
                        new Progress<double>((progressStatus) =>
                        {
                            Console.Write($"Downloading resolution {videoDataResolution.Resolution} ({(progressStatus * 100):N0}%) {videoStreamInfo.Size.MegaBytes:N2} MB\r");
                        })).ConfigureAwait(false);
                    else
                    {
                        var streamInfos = new IStreamInfo[] { audioStreamForMuxInfo, videoStreamInfo };
                        await youTubeClient.Videos.DownloadAsync(
                        streamInfos,
                        new ConversionRequestBuilder(videoDataResolution.DownloadedFilePath).SetFFmpegPath(GetFFmpegPath()).Build(),
                        new Progress<double>((progressStatus) =>
                        {
                            Console.Write($"Downloading and mux resolution {videoDataResolution.Resolution} ({(progressStatus * 100):N0}%) {videoStreamInfo.Size.MegaBytes:N2} MB\r");
                        })).ConfigureAwait(false);
                    }

                    downloaded = true;
                }
                catch { await Task.Delay(3500).ConfigureAwait(false); }
            if (!downloaded)
                throw new InvalidOperationException($"Some error during download of video {videoStreamInfo.Url}");

            // Download thumbnail.
            var thumbnailPath = await DownloadThumbnailAsync(videoManifest, videoStreamInfo.VideoResolution.Height).ConfigureAwait(false);
            videoDataResolution.SetDownloadThumbnail(thumbnailPath);
            Console.WriteLine("");

            videoDataResolution.SetVideoInfo(
                videoName,
                videoStreamInfo.Size.Bytes,
                (int)videoManifest.Duration.Value.TotalSeconds);

            return videoDataResolution;
        }

        private async Task<string?> DownloadThumbnailAsync(
            Video videoManifest,
            int videoHeight)
        {
            // Find thumbnails for specific area
            var url = videoManifest.Thumbnails
                .FirstOrDefault(video => video.Resolution.Height == videoHeight)
                ?.Url;
            url ??= videoManifest.Thumbnails.OrderByDescending(video => video.Resolution.Area).FirstOrDefault()?.Url;
            if (string.IsNullOrWhiteSpace(url))
                return null;

            string filePath = $"{tmpFolder}/{videoManifest.Id}_{videoHeight}.jpg";
            var i = 0;
            while (i < MAX_RETRY)
                try
                {
                    using var httpClient = new HttpClient();
                    var streamGot = await httpClient.GetStreamAsync(url).ConfigureAwait(false);
                    using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                    await streamGot.CopyToAsync(fileStream).ConfigureAwait(false);

                    return filePath;
                }
                catch { await Task.Delay(3500).ConfigureAwait(false); }
            throw new InvalidOperationException($"Some error during download of thumbnail {url}");
        }

        private bool ExistFFmpeg() =>
            File.Exists(GetFFmpegPath());

        private string GetFFmpegPath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return $"{ffMpegFolderPath.TrimEnd('/').TrimEnd('\\')}/ffmpeg.exe";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return $"{ffMpegFolderPath.TrimEnd('/').TrimEnd('\\')}/ffmpeg";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return $"{ffMpegFolderPath.TrimEnd('/').TrimEnd('\\')}/ffmpeg";

            throw new InvalidOperationException("OS not supported");
        }
    }
}