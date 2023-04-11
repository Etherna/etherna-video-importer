using Etherna.BeeNet;
using Etherna.BeeNet.Clients.GatewayApi;
using Etherna.ServicesClient;
using Etherna.VideoImporter.Core.Services;
using Etherna.VideoImporter.Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Net.Http;

namespace Etherna.VideoImporter.Core.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddEthernaVideoImporterService(this IServiceCollection services) =>
            services.AddTransient<EthernaVideoImporter>();

        public static IServiceCollection AddBeeGatewayClientService(this IServiceCollection services) =>
            services.AddTransient<IBeeGatewayClient>((sp) =>
             {
                 var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                 return new BeeGatewayClient(
                     httpClientFactory.CreateClient("ethernaClient"),
                     new Uri(CommonConsts.EthernaGatewayUrl),
                     CommonConsts.BeeNodeGatewayVersion);
             });

        public static IServiceCollection AddBeeNodeClientService(this IServiceCollection services) =>
            services.AddTransient<IBeeNodeClient, BeeNodeClient>();

        public static IServiceCollection AddCleanerVideoService(this IServiceCollection services) =>
            services.AddTransient<ICleanerVideoService, CleanerVideoService>();

        public static IServiceCollection AddEncoderService(this IServiceCollection services) =>
            services.AddTransient<IEncoderService, EncoderService>();

        public static IServiceCollection AddEthernaUserClientsService(this IServiceCollection services) =>
            services.AddTransient<IEthernaUserClients>((sp) =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                return new EthernaUserClients(
                            new Uri(CommonConsts.EthernaCreditUrl),
                            new Uri(CommonConsts.EthernaGatewayUrl),
                            new Uri(CommonConsts.EthernaIndexUrl),
                            new Uri(CommonConsts.EthernaIndexUrl),
                            () => httpClientFactory.CreateClient("ethernaClient"));
            });

        public static IServiceCollection AddGatewayService(this IServiceCollection services, bool useBeeNativeNode) =>
                useBeeNativeNode ?
                    services.AddTransient<IGatewayService, BeeGatewayService>() :
                    services.AddTransient<IGatewayService, EthernaGatewayService>();
        
        public static IServiceCollection AddMigrationService(this IServiceCollection services) =>
            services.AddTransient<IMigrationService, MigrationService>();

        public static IServiceCollection AddVideoUploaderService(this IServiceCollection services) =>
            services.AddTransient<IVideoUploaderService, VideoUploaderService>();

        public static IServiceCollection ConfigureFFMpegSettings(
            this IServiceCollection services,
            string ffMpegBinaryPath,
            string ffMpegFolderPath)
        =>
            services.Configure<FFMpegSettings>(options =>
            {
                options.FFMpegBinaryPath = ffMpegBinaryPath;
                options.FFMpegFolderPath = ffMpegFolderPath;
            });

        public static IServiceCollection ConfigureImportSettings(
            this IServiceCollection services,
            bool deleteExogenousVideos,
            bool deleteVideosMissingFromSource,
            bool forceUploadVideo,
            bool ignoreNewVersionsOfImporter,
            string sourceUri,
            DirectoryInfo tempDirPath,
            bool unpinRemovedVideos,
            bool useBeeNativeNode,
            string userEthAddr)
        =>
            services.Configure<ImporterSettings>(options =>
            {
                options.DeleteExogenousVideos = deleteExogenousVideos;
                options.DeleteVideosMissingFromSource = deleteVideosMissingFromSource;
                options.ForceUploadVideo = forceUploadVideo;
                options.IgnoreNewVersionsOfImporter = ignoreNewVersionsOfImporter;
                options.SourceUri = sourceUri;
                options.TempDirectoryPath = tempDirPath;
                options.UnpinRemovedVideos = unpinRemovedVideos;
                options.UseBeeNativeNode = useBeeNativeNode;
                options.UserEthAddr = userEthAddr;
            });

        public static IHttpClientBuilder AddEthernaHttpClient(
            this IServiceCollection services,
            DelegatingHandler delegatingHandler)
        {
            return services.AddHttpClient("ethernaClient", c =>
            {
                c.Timeout = TimeSpan.FromMinutes(30);
                c.DefaultRequestHeaders.ConnectionClose = true; //fixes https://etherna.atlassian.net/browse/EVI-74
            })
            .ConfigurePrimaryHttpMessageHandler(() => delegatingHandler);
        }

        public static IServiceCollection ConfigureUploadSettings(
            this IServiceCollection services,
            bool acceptPurchaseOfAllBatches,
            bool includeAudioTrack,
            bool offerVideos,
            bool pinVideos,
            bool skip1440,
            bool skip1080,
            bool skip720,
            bool skip480,
            bool skip360,
            int ttlPostageStamp)
        {
            return services.Configure<UploadSettings>(options =>
            {
                options.AcceptPurchaseOfAllBatches = acceptPurchaseOfAllBatches;
                options.IncludeAudioTrack = includeAudioTrack;
                options.OfferVideos = offerVideos;
                options.PinVideos = pinVideos;
                options.Skip1440 = skip1440;
                options.Skip1080 = skip1080;
                options.Skip720 = skip720;
                options.Skip480 = skip480;
                options.Skip360 = skip360;
                options.TTLPostageStamp = TimeSpan.FromSeconds(ttlPostageStamp);
            });
        }
    }
}
