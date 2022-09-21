using CsvHelper;
using Etherna.BeeNet;
using Etherna.EthernaVideoImporter.Config;
using Etherna.EthernaVideoImporter.Services;
using EthernaVideoImporter.Dtos;
using EthernaVideoImporter.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YoutubeDownloader.Clients;

internal class Program
{
    static async Task Main(string[] args)
    {
        const string tmpFolder = "tmpData";

        // Build a config object, using env vars and JSON providers.
        IConfiguration config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();

        // Get values from the config given their key and their target type.
        var beeNodeConfig = config.GetRequiredSection("BeeNode").Get<BeeNodeConfig>();
        var indexUrl = config.GetValue<string>("Etherna:IndexUrl");
        var gatewayUrl = config.GetValue<string>("Etherna:GatewayUrl");

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

        if (!Directory.Exists(tmpFolder))
            Directory.CreateDirectory(tmpFolder);

        // Read csv.
        IEnumerable<VideoDataInfo> videoDataInfoDtos;
        using (var reader = new StreamReader(args[0]))
        using (var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture))
            videoDataInfoDtos = csvReader.GetRecords<VideoDataInfo>().ToList();

        var totalVideo = videoDataInfoDtos.Count();
        Console.WriteLine($"Csv with {totalVideo} items to upload");

        // Call import service for each video.
        var tmpFolderFullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, tmpFolder);
        var videoImporterService = new VideoImporterService(new YoutubeDownloadClient(), tmpFolderFullPath);
        var videoUploaderService = new VideoUploaderService(
            new BeeNodeClient(beeNodeConfig.Url!,
            beeNodeConfig.GatewayPort,
            beeNodeConfig.DebugPort,
            beeNodeConfig.GatewayVersion,
            beeNodeConfig.DebugVersion),
            gatewayUrl,
            indexUrl);

        var videoCount = 0;
        foreach (var videoInfo in videoDataInfoDtos)
        {
            try
            {
                Console.WriteLine($"Start processing video {++videoCount} of {totalVideo}");

                // Download from youtube.
                await videoImporterService.StartAsync(videoInfo).ConfigureAwait(false);

                // Upload on bee node.
                await videoUploaderService.StartAsync(videoInfo).ConfigureAwait(false);

                Console.WriteLine($"Video #{videoCount} processed");
            }
#pragma warning disable CA1031 // Catch general exception types for process next video in case of some error
            catch (Exception ex)
#pragma warning restore CA1031
            {
                Console.WriteLine($"{ex.Message} \n Unable to upload: {videoDataInfoDtos}");
                videoInfo.VideoStatusNote = ex.Message;
            }
            finally
            {
                // Save csv with results at every cycle.
                using var writer = new StreamWriter(args[0]);
                using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
                await csv.WriteRecordsAsync(videoDataInfoDtos).ConfigureAwait(false);
            }
        }
    }
}