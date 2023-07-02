using Etherna.BeeNet;
using Etherna.BeeNet.Clients.GatewayApi;
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
        public static void AddCoreServices(
            this IServiceCollection services,
            Action<EncoderServiceOptions> configureEncoderOptions,
            Action<VideoUploaderServiceOptions> configureVideoUploaderOptions,
            string httpClientName,
            bool useBeeNativeNode)
        {
            // Configure options.
            services.Configure(configureEncoderOptions);
            services.AddSingleton<IValidateOptions<EncoderServiceOptions>, EncoderServiceOptionsValidation>();
            services.Configure(configureVideoUploaderOptions);

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
            //bee.net
            services.AddSingleton<IBeeGatewayClient>((sp) =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                return new BeeGatewayClient(
                    httpClientFactory.CreateClient(httpClientName),
                    new Uri(CommonConsts.EthernaGatewayUrl),
                    CommonConsts.BeeNodeGatewayVersion);
            });
            services.AddSingleton<IBeeNodeClient, BeeNodeClient>();
        }
    }
}
