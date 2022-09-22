using CsvHelper;
using Etherna.BeeNet;
using Etherna.BeeNet.Clients.DebugApi;
using Etherna.BeeNet.Clients.GatewayApi;
using Etherna.EthernaVideoImporter.Services;
using Etherna.EthernaVideoImporter.YoutubeDownloader;
using EthernaVideoImporter.Models;
using EthernaVideoImporter.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

internal class Program
{
    // Consts.
    private const string HelpText =
        "EthernaVideoImporter help:\n\n" +
        "-s\tSource csv filepath to import\n" +
        "-p\tPin video (true/false)\n" +
        "-o\tOutput filepath\n" +
        "\n" +
        "-h\tPrint help\n";
    private const int BEENODE_DEBUGPORT = 2;
    private const DebugApiVersion BEENODE_DEBUGVERSION = DebugApiVersion.v3_0_2;
    private const int BEENODE_GATEWAYPORT = 1;
    private const GatewayApiVersion BEENODE_GATEWAYVERSION = GatewayApiVersion.v3_0_2;
    private const string BEENODE_URL = "http://beenode.ext";
    private const string ETHERNA_INDEX = "http://beenode.ext";
    private const string ETHERNA_GATEWAY = "http://beenode.ext";

    // Methods.
    static async Task Main(string[] args)
    {
        // Parse arguments.
        string? sourceCsvFile = null;
        bool pinVideo = false;
        string? outputFile = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-s": sourceCsvFile = args[++i]; break;
                case "-p": pinVideo = args[++i] == "true"; break;
                case "-o": outputFile = args[++i]; break;
                case "-h": Console.Write(HelpText); return;
                default: throw new ArgumentException(args[i] + " is not a valid argument");
            }
        }

        // Request connection urls.
        Console.WriteLine();
        Console.WriteLine("Source csv filepath to import:");
        sourceCsvFile = ReadStringIfEmpty(sourceCsvFile);

        Console.WriteLine();
        Console.WriteLine("Output filepath:");
        outputFile = ReadStringIfEmpty(outputFile);

        // Check file and tmp folder.
        const string tmpFolder = "tmpData";
        if (!File.Exists(sourceCsvFile))
        {
            Console.WriteLine($"File not found {sourceCsvFile}");
            return;
        }

        if (!Directory.Exists(tmpFolder))
            Directory.CreateDirectory(tmpFolder);
        var tmpFolderFullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, tmpFolder);

        // Load csv data.
        var videoDataInfoDtos = ReadFromCsv(sourceCsvFile, outputFile);
        var totalVideo = videoDataInfoDtos.Count();
        Console.WriteLine($"Csv with {totalVideo} items to upload");


        // Inizialize services.
        using var httpClient = CreateAuthClient();
        var videoImporterService = new VideoImporterService(
            new YoutubeDownloadClient(),
            tmpFolderFullPath);
        var beeNodeClient = new BeeNodeClient(
                BEENODE_URL,
                BEENODE_GATEWAYPORT,
                BEENODE_DEBUGPORT,
                BEENODE_GATEWAYVERSION,
                BEENODE_DEBUGVERSION,
                httpClient);
        var videoUploaderService = new VideoUploaderService(
            beeNodeClient,
            ETHERNA_GATEWAY,
            ETHERNA_INDEX);

        // Call import service for each video.
        var videoCount = 0;
        foreach (var videoInfo in videoDataInfoDtos)
        {
            try
            {
                Console.WriteLine($"Start processing video {++videoCount} of {totalVideo}");

                // Download from youtube.
                await videoImporterService.StartAsync(videoInfo).ConfigureAwait(false);

                // Upload on bee node.
                await videoUploaderService.StartAsync(videoInfo, pinVideo).ConfigureAwait(false);

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
                using var writer = new StreamWriter(outputFile);
                using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
                await csv.WriteRecordsAsync(videoDataInfoDtos).ConfigureAwait(false);
            }
        }
    }

    // Private helpers.
    private static HttpClient CreateAuthClient()
    {
        return new HttpClient();
    }

    private static IEnumerable<VideoInfoWithData> ReadFromCsv(
        string sourceCsvFile,
        string outputFile)
    {
        IEnumerable<VideoInfoWithData> currentSourceVideoInfo;
        using (var reader = new StreamReader(sourceCsvFile))
        using (var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture))
            currentSourceVideoInfo = csvReader.GetRecords<VideoInfoWithData>();

        if (!File.Exists(outputFile))
            return currentSourceVideoInfo;

        // Merge video data with previus runner if outputFile exists.
        IEnumerable<VideoInfoWithData> previusRunnerVideoInfo;
        using (var reader = new StreamReader(outputFile))
        using (var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture))
            previusRunnerVideoInfo = csvReader.GetRecords<VideoInfoWithData>();

        foreach (var previusItem in previusRunnerVideoInfo)
        {
            var currentItem = currentSourceVideoInfo.FirstOrDefault(i => i.YoutubeUrl == previusItem.YoutubeUrl);
            if (currentItem is null)
                continue;

            // Copy only video data
            currentItem.Bitrate = previusItem.Bitrate;
            currentItem.BatchId = previusItem.BatchId;
            currentItem.DownloadedFileName = previusItem.DownloadedFileName;
            currentItem.DownloadedFilePath = previusItem.DownloadedFilePath;
            currentItem.IndexVideoId = previusItem.IndexVideoId;
            currentItem.HashMetadataReference = previusItem.HashMetadataReference;
            currentItem.Quality = previusItem.Quality;
            currentItem.Size = previusItem.Size;
            currentItem.VideoReference = previusItem.VideoReference;
            currentItem.VideoStatus = previusItem.VideoStatus;
            currentItem.VideoStatusNote = previusItem.VideoStatusNote;
        }

        return currentSourceVideoInfo;
    }

    private static string ReadStringIfEmpty(string? strValue)
    {
        if (string.IsNullOrWhiteSpace(strValue))
        {
            while (string.IsNullOrWhiteSpace(strValue))
            {
                strValue = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(strValue))
                    Console.WriteLine("*Empty string not allowed*");
            }
        }
        else Console.WriteLine(strValue);

        return strValue;
    }
}