using CsvHelper;
using Etherna.BeeNet;
using Etherna.BeeNet.Clients.DebugApi;
using Etherna.BeeNet.Clients.GatewayApi;
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
        "-p\tPin video (true/false)\n" +
        "-o\tOutput filepath\n" +
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
        var httpClient = new HttpClient(authResult.RefreshTokenHandler) { Timeout = TimeSpan.FromHours(1) };

        // Inizialize services.
        var videoImporterService = new VideoImporterService(
            new YoutubeDownloadClient(),
            tmpFolderFullPath);
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
            userEthAddr);

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
            catch (Exception ex)
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
        string outputFile)
    {
        IEnumerable<VideoInfoWithData> currentSourceVideoInfo;
        using (var reader = new StreamReader(sourceCsvFile))
        using (var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture))
            currentSourceVideoInfo = csvReader.GetRecords<VideoInfoWithData>().ToList();

        if (!File.Exists(outputFile))
            return currentSourceVideoInfo;

        // Merge video data with previus runner if outputFile exists.
        IEnumerable<VideoInfoWithData> previusRunnerVideoInfo;
        using (var reader = new StreamReader(outputFile))
        using (var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture))
            previusRunnerVideoInfo = csvReader.GetRecords<VideoInfoWithData>().ToList();

        foreach (var previusItem in previusRunnerVideoInfo)
        {
            var currentItem = currentSourceVideoInfo.FirstOrDefault(i => i.YoutubeUrl == previusItem.YoutubeUrl);
            if (currentItem is null)
                continue;

            // Copy only video data
            currentItem.Bitrate = previusItem.Bitrate;
            currentItem.BatchId = previusItem.BatchId;
            currentItem.BatchReferenceId = previusItem.BatchReferenceId;
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