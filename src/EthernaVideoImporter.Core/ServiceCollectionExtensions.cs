// Copyright 2022-present Etherna SA
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Etherna.VideoImporter.Core.Options;
using Etherna.VideoImporter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Etherna.VideoImporter.Core
{
    public static class ServiceCollectionExtensions
    {
        public static void AddCoreServices(
            this IServiceCollection services,
            Action<EncoderServiceOptions> configureEncoderOptions,
            Action<FFmpegServiceOptions> configureFFmpegOptions,
            Action<VideoUploaderServiceOptions> configureVideoUploaderOptions,
            bool useBeeNativeNode)
        {
            // Configure options.
            services.Configure(configureEncoderOptions);
            services.Configure(configureFFmpegOptions);
            services.Configure(configureVideoUploaderOptions);

            // Add transient services.
            services.AddTransient<IAppVersionService, AppVersionService>();
            services.AddTransient<IEthernaVideoImporter, EthernaVideoImporter>();

            services.AddTransient<ICleanerVideoService, CleanerVideoService>();
            services.AddTransient<IEncoderService, EncoderService>();
            if (useBeeNativeNode)
                services.AddTransient<IGatewayService, BeeGatewayService>();
            else
                services.AddTransient<IGatewayService, EthernaGatewayService>();
            services.AddTransient<IMigrationService, MigrationService>();
            services.AddTransient<IIoService, ConsoleIoService>();
            services.AddTransient<IVideoUploaderService, VideoUploaderService>();

            // Add singleton services.
            services.AddSingleton<IFFmpegService, FFmpegService>();
        }
    }
}
