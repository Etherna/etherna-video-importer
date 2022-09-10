using CsvHelper;
using EthernaVideoImporter.Dtos;
using EthernaVideoImporter.Services;
using System.Globalization;
using YoutubeDownloader.Clients;

internal class Program
{
    static async Task Main(string[] args)
    {
        string tmpFolder = "tmpData";

        if (args is null ||
            args.Length < 0)
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
        IEnumerable<VideoDataInfoDto> videoDataInfoDtos;
        using (var reader = new StreamReader(args[0]))
        using (var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture))
        {
            videoDataInfoDtos = csvReader.GetRecords<VideoDataInfoDto>().ToList();
        }
        var totalVideo = videoDataInfoDtos.Count();
        Console.WriteLine($"Csv with {totalVideo} items to upload");

        // Call import service for each video.
        var youtubeDownloadClient = new YoutubeDownloadClient();
        var videoImporterService = new VideoImporterService(youtubeDownloadClient, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, tmpFolder));

        var videoCount = 0;
        foreach (var videoInfo in videoDataInfoDtos)
        {
            try
            {
                Console.WriteLine($"Start processing video {++videoCount} of {totalVideo}");
                await videoImporterService.Start(videoInfo);
                Console.WriteLine($"Video #{videoCount} processed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message} \n Unable to upload: {videoDataInfoDtos}");
                videoInfo.VideoStatusNote = ex.Message;
            }
        }

        // Save csv with results.
        using (var writer = new StreamWriter(args[0]))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            await csv.WriteRecordsAsync(videoDataInfoDtos);

    }
}