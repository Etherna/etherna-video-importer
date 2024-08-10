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

using Etherna.BeeNet.Models;
using Etherna.Sdk.Users.Index.Models;
using Etherna.UniversalFiles;
using Etherna.VideoImporter.Core.Models.Domain;
using M3U8Parser;
using System;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public interface IParsingService
    {
        Task<ThumbnailFile[]> ParseThumbnailFromPublishedVideoManifestAsync(
            PublishedVideoManifest publishedManifest);
        
        Task<HlsVideoEncoding> ParseVideoEncodingFromHlsMasterPlaylistFileAsync(
            TimeSpan duration,
            FileBase masterFile,
            SwarmAddress? masterSwarmAddress,
            MasterPlaylist masterPlaylist);

        Task<VideoEncodingBase> ParseVideoEncodingFromPublishedVideoManifestAsync(
            PublishedVideoManifest publishedManifest);

        Task<VideoEncodingBase> ParseVideoEncodingFromUUriAsync(
            BasicUUri mainFileUri,
            SwarmAddress? swarmAddress = null);

        Task<HlsVideoVariant> ParseVideoVariantFromHlsStreamPlaylistFileAsync(
            FileBase streamPlaylistFile,
            SwarmAddress? streamPlaylistSwarmAddress,
            int height,
            int width);

        Task<MasterPlaylist?> TryParseHlsMasterPlaylistFromFileAsync(FileBase hlsPlaylist);
    }
}