using Etherna.BeeNet;
using Etherna.BeeNet.Clients.GatewayApi;
using Etherna.ServicesClient;
using Etherna.VideoImporter.Core.Options;
using Etherna.VideoImporter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;

namespace Etherna.VideoImporter.Core
{
    public static class ServiceCollectionExtensions
    {
        private const string EthernaServicesHttpClientName = "ethernaClient";

        public static void AddCoreServices(
            this IServiceCollection services,
            Action<EncoderServiceOptions> configureEncoderOptions,
            Action<VideoUploaderServiceOptions> configureVideoUploaderOptions,
            bool useBeeNativeNode,
            HttpMessageHandler ethernaServicesHttpMessageHandler)
        {
            // Configure options.
            services.Configure(configureEncoderOptions);
            services.AddSingleton<IValidateOptions<EncoderServiceOptions>, EncoderServiceOptionsValidation>();
            services.Configure(configureVideoUploaderOptions);
            services.AddSingleton<IValidateOptions<VideoUploaderServiceOptions>, VideoUploaderServiceOptionsValidation>();

            // Add transient services.
            services.AddTransient<IEthernaVideoImporter, EthernaVideoImporter>();

            services.AddTransient<ICleanerVideoService, CleanerVideoService>();
            services.AddTransient<IEncoderService, EncoderService>();
            if (useBeeNativeNode)
                services.AddTransient<IGatewayService, BeeGatewayService>();
            else
                services.AddTransient<IGatewayService, EthernaGatewayService>();
            services.AddTransient<IMigrationService, MigrationService>();
            services.AddTransient<IVideoUploaderService, VideoUploaderService>();

            // Add singleton services.
            //etherna services user client
            services.AddSingleton<IEthernaUserClients>((sp) =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                return new EthernaUserClients(
                    new Uri(CommonConsts.EthernaCreditUrl),
                    new Uri(CommonConsts.EthernaGatewayUrl),
                    new Uri(CommonConsts.EthernaIndexUrl),
                    new Uri(CommonConsts.EthernaIndexUrl),
                    () => httpClientFactory.CreateClient(EthernaServicesHttpClientName));
            });

            //bee.net
            services.AddSingleton<IBeeGatewayClient>((sp) =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                return new BeeGatewayClient(
                    httpClientFactory.CreateClient(EthernaServicesHttpClientName),
                    new Uri(CommonConsts.EthernaGatewayUrl),
                    CommonConsts.BeeNodeGatewayVersion);
            });
            services.AddSingleton<IBeeNodeClient, BeeNodeClient>();

            // Add http clients.
            services.AddHttpClient(EthernaServicesHttpClientName, c =>
            {
                c.Timeout = TimeSpan.FromMinutes(30);
                c.DefaultRequestHeaders.ConnectionClose = true; //fixes https://etherna.atlassian.net/browse/EVI-74
            })
            .ConfigurePrimaryHttpMessageHandler(() => ethernaServicesHttpMessageHandler);
        }
    }
}
