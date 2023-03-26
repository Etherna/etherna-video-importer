using Etherna.VideoImporter.Core.Models.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public interface IMuxingService
    {
        ValueTask TranscodeVideoAsync(
            VideoLocalFile sourceVideo,
            string resolutionTarget,
            IProgress<double>? progress,
            CancellationToken cancellationToken = default);
    }
}
