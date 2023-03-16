using System;
using System.Text.RegularExpressions;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public partial class VideoSwarmFile : SwarmFileBase
    {
        // Consts.
        [GeneratedRegex("^(?<label>\\d+p)\\d*$")]
        private static partial Regex QualityLabelRegex();

        // Constructor.
        public VideoSwarmFile(
            long byteSize,
            string videoQualityLabel,
            Uri uri)
            : base(uri)
        {
            var originVideoQualityLabelMatch = QualityLabelRegex().Match(videoQualityLabel);
            if (originVideoQualityLabelMatch.Success)
                VideoQualityLabel = originVideoQualityLabelMatch.Groups["label"].Value;
            else
                throw new ArgumentException("Invalid quality label", nameof(videoQualityLabel));

            ByteSize = byteSize;
        }

        // Properties.
        public long ByteSize { get; }
        public string VideoQualityLabel { get; }
    }
}
