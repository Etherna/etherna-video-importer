using CsvHelper;
using Etherna.BeeNet;
using Etherna.BeeNet.Clients.DebugApi;
using Etherna.BeeNet.Clients.GatewayApi;
using Etherna.EthernaVideoImporter.Dtos;
using Etherna.EthernaVideoImporter.Services;
using EthernaVideoImporter.Dtos;
using EthernaVideoImporter.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using YoutubeDownloader.Clients;

internal class Program
{
    static async Task Main(string[] args)
    {
        string tmpFolder = "tmpData";

        if (args is null ||
            args.Length < 1)
        {
            Console.WriteLine("Missing read path");
            return;
        }
        if (!File.Exists(args[0]))
        {
            Console.WriteLine($"File not found {args[0]}");
            return;
        }
        if (args.Length < 2)
        {
            Console.WriteLine("Missing beenode url");
            return;
        }
        if (args.Length < 3)
        {
            Console.WriteLine("Missing beenode port");
            return;
        }
        if (args.Length < 4)
        {
            Console.WriteLine("Missing beenode version");
            return;
        }
        var beeNodeUrl = args[1];
#pragma warning disable CA1305 // Specify IFormatProvider
        var beeNodePort = Convert.ToInt32(args[2]);
#pragma warning restore CA1305 // Specify IFormatProvider
        var beeNodeDebugPort = beeNodePort + 1;
        var beeNodeVersion = GatewayApiVersion.v3_0_2;
        var beeNodeDebugVersion = DebugApiVersion.v3_0_2;

        if (!Directory.Exists(tmpFolder))
            Directory.CreateDirectory(tmpFolder);

        // Read csv.
        IEnumerable<VideoDataInfoDto> videoDataInfoDtos;
        using (var reader = new StreamReader(args[0]))
        using (var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture))
        {
            videoDataInfoDtos = csvReader.GetRecords<VideoDataInfoDto>().ToList();
        }
        var totalVideo = videoDataInfoDtos.Count();
        Console.WriteLine($"Csv with {totalVideo} items to upload");

        // Call import service for each video.
        var tmpFolderFullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, tmpFolder);
        var videoImporterService = new VideoImporterService(new YoutubeDownloadClient(), tmpFolderFullPath);
        var videoUploaderService = new VideoUploaderService(new BeeNodeClient(beeNodeUrl, beeNodePort, beeNodeDebugPort, beeNodeVersion, beeNodeDebugVersion), tmpFolderFullPath);

        var videoCount = 0;
        foreach (var videoInfo in videoDataInfoDtos)
        {
            try
            {
                Console.WriteLine($"Start processing video {++videoCount} of {totalVideo}");

                // Download from youtube.
                var downloadInfo = await videoImporterService.Start(videoInfo);

                if (downloadInfo != null)
                    // Upload on bee.
                    await videoUploaderService.Start(videoInfo, downloadInfo);

                Console.WriteLine($"Video #{videoCount} processed");
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Console.WriteLine($"{ex.Message} \n Unable to upload: {videoDataInfoDtos}");
                videoInfo.VideoStatusNote = ex.Message;
            }
            finally
            {
                // Save csv with results at every cycle.
                using (var writer = new StreamWriter(args[0]))
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                    await csv.WriteRecordsAsync(videoDataInfoDtos).ConfigureAwait(false);
            }
        }
    }
}