using System;
using System.Text.RegularExpressions;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public partial class VideoSwarmFile : SwarmFileBase, IVideoFile
    {
        // Consts.
        [GeneratedRegex("^(?<label>\\d+p)\\d*$")]
        private static partial Regex QualityLabelRegex();

        // Constructor.
        public VideoSwarmFile(
            long byteSize,
            string videoQualityLabel,
            string hash)
            : base(hash, byteSize)
        {
            var originVideoQualityLabelMatch = QualityLabelRegex().Match(videoQualityLabel);
            if (originVideoQualityLabelMatch.Success)
                VideoQualityLabel = originVideoQualityLabelMatch.Groups["label"].Value;
            else
                throw new ArgumentException("Invalid quality label", nameof(videoQualityLabel));
        }

        // Properties.
        public string VideoQualityLabel { get; }
    }
}
