using Etherna.EthernaVideoImporterLibrary.Models;
using Etherna.ServicesClient.Clients.Index;
using System.Threading.Tasks;

namespace Etherna.EthernaVideoImporterLibrary.Services
{
    /// <summary>
    /// Uploader services
    /// </summary>
    public interface IVideoUploaderService
    {
        /// <summary>
        /// Start to upload all video data (manifest, video with all avaiable resolutions, thumbnail, index).
        /// </summary>
        /// <param name="videoData">all video data</param>
        /// <param name="pinVideo">pin video</param>
        /// <param name="offerVideo">free video</param>
        public Task UploadVideoAsync(
            VideoData videoData,
            bool pinVideo,
            bool offerVideo);

        /// <summary>
        /// Update metadata and index.
        /// </summary>
        /// <param name="videoManifestDto">manifest data</param>
        /// <param name="videoData">video data</param>
        /// <param name="pinVideo">free video</param>
        Task<string> UploadMetadataAsync(
            VideoManifestDto videoManifestDto,
            VideoData videoData,
            bool pinVideo);
    }
}
