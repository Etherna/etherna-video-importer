using Etherna.BeeNet;
using Etherna.BeeNet.InputModels;
using Etherna.EthernaVideoImporter.Dtos;
using EthernaVideoImporter.Dtos;
using JavaScriptEngineSwitcher.Core.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YoutubeDownloader.Dto;

namespace Etherna.EthernaVideoImporter.Services
{
    internal class VideoUploaderService
    {
        private readonly BeeNodeClient beeNodeClient;
        private readonly string tmpFolder;

        // Constractor.
        public VideoUploaderService(
            BeeNodeClient beeNodeClient,
            string tmpFolder)
        {
            if (beeNodeClient is null)
                throw new ArgumentNullException(nameof(beeNodeClient));
            if (string.IsNullOrWhiteSpace(tmpFolder))
                throw new ArgumentNullException(nameof(tmpFolder));

            this.beeNodeClient = beeNodeClient;
            this.tmpFolder = tmpFolder;
        }

        // Public methods.
        public async Task Start(VideoDataInfoDto videoDataInfoDto, VideoDownloadInfo videoDownloadInfo)
        {
            if (videoDataInfoDto.VideoStatus == VideoStatus.Processed)
                return;
            if (videoDataInfoDto.VideoStatus != VideoStatus.Downloaded)
                throw new InvalidOperationException("Invalid Status");


            // Create batch.
            var postageBatch = await beeNodeClient.DebugClient!.BuyPostageBatchAsync(1, 28);
            await Task.Delay(90000);

            // Upload file.
            var fileParameterInput = new FileParameterInput(
                File.OpenRead(videoDataInfoDto.DownloadedFilePath!), 
                Path.GetFileName(videoDataInfoDto.DownloadedFilePath!), 
                MimeTypes.GetMimeType(Path.GetFileName(videoDataInfoDto.DownloadedFilePath!)));
            var reference = await beeNodeClient.GatewayClient!.UploadFileAsync(postageBatch, files: new List<FileParameterInput> { fileParameterInput }, swarmCollection: false);

            // Upload metadata.
            var metadataVideoDto = new MetadataVideoDto(
                postageBatch, 
                videoDataInfoDto.Description ?? "", 
                videoDataInfoDto.Duration,
                -1L, //Created at
                videoDownloadInfo.Quality, 
                "owner", 
                new List<MetadataVideoSource> { new MetadataVideoSource(videoDownloadInfo.Bitrate, videoDownloadInfo.Quality, reference, videoDownloadInfo.Size) },
                null,
                videoDataInfoDto.Title ?? "",
                -1); //Updated at


            videoDataInfoDto.VideoStatus = VideoStatus.Processed;
            return;
        }

        // Private methods.

    }
}
