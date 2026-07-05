// Copyright 2022-present Etherna SA
// This file is part of Etherna Video Importer.
// 
// Etherna Video Importer is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// Etherna Video Importer is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with Etherna Video Importer.
// If not, see <https://www.gnu.org/licenses/>.

using Etherna.Sdk.Tools.UniversalFiles;
using Etherna.Sdk.Tools.UniversalFiles.Extensions;
using Etherna.Sdk.Tools.Video.Services;
using Etherna.Sdk.Users.Gateway.Services;
using Etherna.SwarmSdk;
using Etherna.SwarmSdk.Hashing;
using Etherna.SwarmSdk.Services;
using Etherna.VideoImporter.Core.Options;
using Etherna.VideoImporter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Http;

namespace Etherna.VideoImporter.Core
{
    public static class ServiceCollectionExtensions
    {
        public static void AddCoreServices(
            this IServiceCollection services,
            Action<EthernaVideoImporterOptions> configureVideoImporterOptions,
            Action<CleanerVideoServiceOptions> configureCleanerOptions,
            Action<EncoderServiceOptions> configureEncoderOptions,
            Action<FFmpegServiceOptions> configureFFmpegOptions,
            Action<VideoUploaderServiceOptions> configureVideoUploaderOptions)
        {
            // Configure options.
            services.Configure(configureCleanerOptions);
            services.Configure(configureEncoderOptions);
            services.Configure(configureFFmpegOptions);
            services.Configure(configureVideoImporterOptions);
            services.Configure(configureVideoUploaderOptions);

            // Add transient services.
            services.AddTransient<IAppVersionService, AppVersionService>();
            services.AddTransient<IChunkService, ChunkService>();
            services.AddTransient<ICleanerVideoService, CleanerVideoService>();
            services.AddTransient<IEncodingService, EncodingService>();
            services.AddTransient<IEthernaVideoImporter, EthernaVideoImporter>();
            services.AddTransient<IGatewayService, GatewayService>();
            services.AddTransient<Hasher>();
            services.AddTransient<IHlsService, HlsService>();
            services.AddTransient<IMigrationService, MigrationService>();
            services.AddTransient<IIoService, ConsoleIoService>();
            services.AddTransient<IVideoManifestService, VideoManifestService>();
            services.AddTransient<IVideoUploaderService, VideoUploaderService>();

            // Add singleton services.
            services.AddSingleton<IFFmpegService, FFmpegService>();
            services.AddSingleton<IUFileProvider>(sp =>
            {
                var beeClient = sp.GetRequiredService<ISwarmClient>();
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();

                var provider = new UFileProvider(httpClientFactory);
                provider.UseSwarmUFiles(beeClient);
                return provider;
            });
        }
    }
}
