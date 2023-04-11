using Etherna.BeeNet;
using Etherna.BeeNet.Clients.GatewayApi;
using Etherna.ServicesClient;
using Etherna.VideoImporter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Http;

namespace Etherna.VideoImporter.Core.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddImportSettings(
            this IServiceCollection services,
            bool acceptPurchaseOfAllBatches,
            bool deleteExogenousVideos,
            bool deleteVideosMissingFromSource,
            string ffMpegBinaryPath,
            string ffMpegFolderPath,
            bool forceUploadVideo,
            bool ignoreNewVersionsOfImporter,
            bool includeAudioTrack,
            bool offerVideos,
            bool pinVideos,
            string sourceUri,
            bool skip1440,
            bool skip1080,
            bool skip720,
            bool skip480,
            bool skip360,
            string tempDirectoryPath,
            int ttlPostageStamp,
            bool unpinRemovedVideos,
            string userEthAddr)
        {
            return services.Configure<ImporterSettings>(options =>
            {
                options.AcceptPurchaseOfAllBatches = acceptPurchaseOfAllBatches;
                options.DeleteExogenousVideos = deleteExogenousVideos;
                options.DeleteVideosMissingFromSource = deleteVideosMissingFromSource;
                options.FFMpegBinaryPath = ffMpegBinaryPath;
                options.FFMpegFolderPath = ffMpegFolderPath;
                options.ForceUploadVideo = forceUploadVideo;
                options.IgnoreNewVersionsOfImporter = ignoreNewVersionsOfImporter;
                options.IncludeAudioTrack = includeAudioTrack;
                options.OfferVideos = offerVideos;
                options.PinVideos = pinVideos;
                options.SourceUri = sourceUri;
                options.Skip1440 = skip1440;
                options.Skip1080 = skip1080;
                options.Skip720 = skip720;
                options.Skip480 = skip480;
                options.Skip360 = skip360;
                options.TempDirectoryPath = tempDirectoryPath;
                options.TTLPostageStamp = TimeSpan.FromSeconds(ttlPostageStamp);
                options.UnpinRemovedVideos = unpinRemovedVideos;
                options.UserEthAddr = userEthAddr;
            });
        }

        public static IServiceCollection AddCommonServices(
            this IServiceCollection services, 
            bool useBeeNativeNode)
        {
            services
            .AddTransient<EthernaVideoImporter>()
            .AddTransient<IBeeGatewayClient, BeeGatewayClientWrapper>()
            .AddTransient<IBeeNodeClient, BeeNodeClient>()
            .AddTransient<ICleanerVideoService, CleanerVideoService>()
            .AddTransient<IEthernaUserClients, EthernaUserClientsWrapper>()
            .AddTransient<IEncoderService, EncoderService>()
            .AddTransient<IMigrationService, MigrationService>()
            .AddTransient<IVideoUploaderService, VideoUploaderService>();

            return useBeeNativeNode ?
                services.AddTransient<IGatewayService, BeeGatewayService>() :
                services.AddTransient<IGatewayService, EthernaGatewayService>();
        }

        public static IHttpClientBuilder ConfigureHttpClient(
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
    }
}
