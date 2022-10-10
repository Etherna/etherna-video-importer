using CsvHelper;
using Etherna.BeeNet;
using Etherna.BeeNet.Clients.DebugApi;
using Etherna.BeeNet.Clients.GatewayApi;
using Etherna.EthernaVideoImporter.Models;
using Etherna.EthernaVideoImporter.Services;
using Etherna.EthernaVideoImporter.SSO;
using Etherna.EthernaVideoImporter.YoutubeDownloader;
using EthernaVideoImporter.Models;
using EthernaVideoImporter.Services;
using IdentityModel.OidcClient;
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
        "-o\tOutput filepath that contains all import history\n" +
        "-m\tMax file video size (Mb)\n" +
        "-f\tFree video offer by creator\n" +
        "-p\tPin video\n" +
        "-u\tForce metadata update for all videos that already have it\n" +
        "\n" +
        "-h\tPrint help\n";
    private const int BEENODE_GATEWAYPORT = 443;
    private const GatewayApiVersion BEENODE_GATEWAYVERSION = GatewayApiVersion.v3_0_2;
    private const string BEENODE_URL = "https://gateway.etherna.io/";
    private const string ETHERNA_INDEX = "https://index.etherna.io/";
    private const string ETHERNA_GATEWAY = "https://gateway.etherna.io/";

    // Methods.
    static async Task Main(string[] args)
    {
        // Parse arguments.
        string? sourceCsvFile = null;
        string? maxFilesizeStr = null;
        bool offerVideo = false;
        bool pinVideo = false;
        bool forceUpdateMetadata = false;
        string? outputFile = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-s": sourceCsvFile = args[++i]; break;
                case "-o": outputFile = args[++i]; break;
                case "-m": maxFilesizeStr = args[++i]; break;
                case "-f": offerVideo = true; break;
                case "-p": pinVideo = true; break;
                case "-u": forceUpdateMetadata = true; break;
                case "-h": Console.Write(HelpText); return;
                default: throw new ArgumentException(args[i] + " is not a valid argument");
            }
        }

        // Request input data.
        Console.WriteLine();
        Console.WriteLine("Source csv filepath to import:");
        sourceCsvFile = ReadStringIfEmpty(sourceCsvFile);

        Console.WriteLine();
        Console.WriteLine("Output filepath:");
        outputFile = ReadStringIfEmpty(outputFile);

        int? maxFilesize = null;
        if (!string.IsNullOrWhiteSpace(maxFilesizeStr))
            if (!int.TryParse(maxFilesizeStr, out int convertedFileSize))
                Console.WriteLine("Invalid input for max filesize, will be used unlimited size");
            else
                maxFilesize = convertedFileSize;

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
        var videoDataInfoDtos = ReadFromCsv(sourceCsvFile, outputFile, forceUpdateMetadata);
        var totalVideo = videoDataInfoDtos.Count();
        var itemsToAdd = videoDataInfoDtos.Where(item => item.CsvItemStatus == CsvItemStatus.Added).Count();
        var itemsChanged = videoDataInfoDtos.Where(item => item.CsvItemStatus == CsvItemStatus.Unchanged).Count();
        var itemsImported = videoDataInfoDtos
                            .Where(item => item.ImportStatus == ImportStatus.Processed &&
                                            item.CsvItemStatus == CsvItemStatus.Unchanged)
                            .Count();
        var itemsToImport = totalVideo - itemsImported;
        Console.WriteLine();
        Console.WriteLine($"Csv with {totalVideo} items");
        Console.WriteLine($"New items added: {itemsToAdd}");
        Console.WriteLine($"Items with metadata changed: {itemsChanged}");
        Console.WriteLine($"Items already imported: {itemsImported}");
        Console.WriteLine($"Items to import: {itemsToImport}");
        Console.WriteLine();

        // Sign with SSO and create auth client.
        var authResult = await SigInSSO().ConfigureAwait(false);
        if (authResult.IsError)
        {
            Console.WriteLine($"Error during authentication");
            Console.WriteLine(authResult.Error);
            return;
        }
        var userEthAddr = authResult.User.Claims.Where(i => i.Type == "ether_address").FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(userEthAddr))
        {
            Console.WriteLine($"Missing ether address");
            return;
        }
        var httpClient = new HttpClient(authResult.RefreshTokenHandler) { Timeout = TimeSpan.FromHours(2) };

        // Inizialize services.
        var videoImporterService = new VideoImporterService(
            new YoutubeDownloadClient(),
            tmpFolderFullPath,
            maxFilesize);
        var beeNodeClient = new BeeNodeClient(
                BEENODE_URL,
                BEENODE_GATEWAYPORT,
                null,
                BEENODE_GATEWAYVERSION,
                DebugApiVersion.v3_0_2,
                httpClient);
        var videoUploaderService = new VideoUploaderService(
            httpClient,
            beeNodeClient,
            ETHERNA_GATEWAY,
            ETHERNA_INDEX,
            userEthAddr,
            offerVideo);

        // Call import service for each video.
        var videoCount = 0;
        foreach (var videoInfo in videoDataInfoDtos)
        {
            try
            {
                Console.WriteLine("===============================");
                Console.WriteLine($"Start processing video #{++videoCount} of #{totalVideo}");
                Console.WriteLine($"Title: {videoInfo.Title}");
                Console.WriteLine($"Source Video: {videoInfo.YoutubeUrl}");

                // Download from youtube.
                await videoImporterService.StartAsync(videoInfo).ConfigureAwait(false);

                // Upload on bee node.
                await videoUploaderService.StartAsync(videoInfo, pinVideo).ConfigureAwait(false);

                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine($"#{videoCount} Video imported successfully");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"Error:{ex.Message} \n#{videoCount} Video unable to import\n");
                Console.ResetColor();
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
    private static async Task<LoginResult> SigInSSO()
    {
        // create a redirect URI using an available port on the loopback address.
        // requires the OP to allow random ports on 127.0.0.1 - otherwise set a static port
        var browser = new SystemBrowser(59100);
        var redirectUri = $"http://127.0.0.1:{browser.Port}";

        var options = new OidcClientOptions
        {
            Authority = "https://sso.etherna.io/",
            ClientId = "ethernaVideoImporterId",
            RedirectUri = redirectUri,
            Scope = "openid profile offline_access ether_accounts userApi.gateway userApi.index",
            FilterClaims = false,

            Browser = browser,
            IdentityTokenValidator = new JwtHandlerIdentityTokenValidator(),
            RefreshTokenInnerHttpHandler = new SocketsHttpHandler()
        };

        var oidcClient = new OidcClient(options);
        return await oidcClient.LoginAsync(new LoginRequest()).ConfigureAwait(false);
    }

    private static IEnumerable<VideoInfoWithData> ReadFromCsv(
        string sourceCsvFile,
        string outputFile,
        bool forceUpdateMetadata)
    {
        List<VideoInfoWithData> currentSourceVideoInfo;
        using (var reader = new StreamReader(sourceCsvFile))
        using (var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture))
            currentSourceVideoInfo = csvReader.GetRecords<VideoInfoWithData>().ToList();

        if (!File.Exists(outputFile))
            return currentSourceVideoInfo;

        // Merge video data with previus runner if outputFile exists.
        IEnumerable<VideoInfoWithData> historyVideoInfo;
        using (var reader = new StreamReader(outputFile))
        using (var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture))
            historyVideoInfo = csvReader.GetRecords<VideoInfoWithData>().ToList();

        // Set Added status for new element in .csv that never processed in history .csv
        foreach (var currentItem in currentSourceVideoInfo)
        {
            var previusItem = historyVideoInfo.FirstOrDefault(i => i.YoutubeUrl == currentItem.YoutubeUrl);
            if (previusItem is null)
                currentItem.CsvItemStatus = CsvItemStatus.Added;
        }

        // Restore old values for element in .csv that are already processed in history .csv
        foreach (var historyItem in historyVideoInfo)
        {
            var currentItem = currentSourceVideoInfo.FirstOrDefault(i => i.YoutubeUrl == historyItem.YoutubeUrl);

            // Add to currentSourceVideoInfo the old item imported.
            if (currentItem is null)
            {
                historyItem.CsvItemStatus = CsvItemStatus.Unchanged;
                currentSourceVideoInfo.Add(historyItem);
                continue;
            }

            // Copy only video data.
            currentItem.Bitrate = historyItem.Bitrate;
            currentItem.BatchId = historyItem.BatchId;
            currentItem.BatchReferenceId = historyItem.BatchReferenceId;
            currentItem.DownloadedFileName = historyItem.DownloadedFileName;
            currentItem.DownloadedFilePath = historyItem.DownloadedFilePath;
            currentItem.IndexVideoId = historyItem.IndexVideoId;
            currentItem.HashMetadataReference = historyItem.HashMetadataReference;
            currentItem.Quality = historyItem.Quality;
            currentItem.Size = historyItem.Size;
            currentItem.VideoReference = historyItem.VideoReference;
            currentItem.ImportStatus = historyItem.ImportStatus;
            currentItem.VideoStatusNote = historyItem.VideoStatusNote;
            currentItem.EmbedDecentralizedLink = historyItem.EmbedDecentralizedLink;
            currentItem.EmbedIndexLink = historyItem.EmbedIndexLink;

            // Set property status.
            if (IsChangedAnyData(currentItem, historyItem) ||
                forceUpdateMetadata)
                currentItem.CsvItemStatus = CsvItemStatus.MetadataModified;
            else
                currentItem.CsvItemStatus = CsvItemStatus.Unchanged;
        }

        return currentSourceVideoInfo;
    }

    private static bool IsChangedAnyData(
        VideoInfoWithData currentCsv,
        VideoInfoWithData historyCsv)
    {
        return currentCsv.Title != historyCsv.Title ||
                currentCsv.Description != historyCsv.Description;
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