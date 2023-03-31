using Etherna.VideoImporter.Core.Models.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public interface IMuxingService
    {
        IEnumerable<VideoLocalFile> TranscodeVideos(
            VideoLocalFile videoLocalFile,
            AudioLocalFile audioLocalFile,
            DirectoryInfo importerTempDirectoryInfo);
    }
}
