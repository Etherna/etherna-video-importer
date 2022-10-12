using CsvHelper;
using Etherna.EthernaVideoImporter.SSO;
using EthernaVideoImporter.Models;
using EthernaVideoImporterChecker;
using IdentityModel.OidcClient;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

internal class Program
{
    // Consts.
    private const string HelpText =
        "EthernaVideoImporterChecker help:\n\n" +
        "-s\tSource csv filepath to import\n" +
        "-o\tOutput filepath that contains all import history\n" +
        "-r\tRead only\n" +
        "\n" +
        "-h\tPrint help\n";
    private const string INDEX_API_CREATEBATCH = "api/v0.3/videos";
    private const string ETHERNA_INDEX = "https://index.etherna.io/";
    static readonly JsonSerializerOptions options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Methods.
    static async Task Main(string[] args)
    {
        // Parse arguments.
        string? sourceCsvFile = null;
        //bool readOnly = false;
        string? outputFile = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-s": sourceCsvFile = args[++i]; break;
                case "-o": outputFile = args[++i]; break;
                //case "-r": readOnly = true; break;
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

        var videoDataInfoDtos = ReadFromCsv(sourceCsvFile, outputFile);

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
        using var httpClient = new HttpClient(authResult.RefreshTokenHandler) { Timeout = TimeSpan.FromHours(2) };

        // Call check for each video.
        var totalVideo = videoDataInfoDtos.Count();
        var videoCount = 0;
        foreach (var videoInfo in videoDataInfoDtos)
        {
            try
            {
                Console.WriteLine("===============================");
                Console.WriteLine($"Start checking video #{++videoCount} of #{totalVideo} IpfsHash: {videoInfo.IpfsHash}\tIndex: {videoInfo.IndexVideoId}");

                if (string.IsNullOrWhiteSpace(videoInfo.IndexVideoId))
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"Error: Index video id is NULL");
                    Console.ResetColor();
                    continue;
                }

                var videoIndex = await GetIndexInfoAsync(httpClient, videoInfo.IndexVideoId).ConfigureAwait(false);
                if (videoIndex is null)
                    continue;

                if (videoIndex.LastValidManifest is null)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"Error: Missing valid manifest");
                    Console.ResetColor();
                    continue;
                }

                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine($"#{videoCount} Video check successfully");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"Error:{ex.Message} \n#{videoCount} Unable to check status\n");
                Console.ResetColor();
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
    private static async Task<VideoDto?> GetIndexInfoAsync(
        HttpClient httpClient,
        string videoIndexIdReference)
    {
        var httpGetResponse = await httpClient.GetAsync(new Uri(ETHERNA_INDEX + INDEX_API_CREATEBATCH + $"/{videoIndexIdReference}")).ConfigureAwait(false);
        if (!httpGetResponse.IsSuccessStatusCode)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine($"Error: Response index error code {httpGetResponse.StatusCode}");
            Console.ResetColor();
            return null;
        }

        var responseText = await httpGetResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonSerializer.Deserialize<VideoDto>(responseText, options);
    }

    private static IEnumerable<VideoInfoWithData> ReadFromCsv(
        string sourceCsvFile,
        string outputFile)
    {
        List<VideoInfoWithData> currentSourceVideoInfo;
        using (var reader = new StreamReader(sourceCsvFile))
        using (var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture))
            currentSourceVideoInfo = csvReader.GetRecords<VideoInfoWithData>().ToList();

        return currentSourceVideoInfo.Where(i => i.ImportStatus == ImportStatus.Processed).ToList();
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

}