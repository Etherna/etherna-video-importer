namespace Etherna.VideoImporter.Core.Models.SwarmDtos
{
    public class PostageBatchDto
    {
        public string Id { get; init; } = default!;
        public long BatchTTL { get; init; }
        public int BlockNumber { get; init; }
        public int BucketDepth { get; init; }
        public int Depth { get; init; }
        public bool Exists { get; init; }
        public bool ImmutableFlag { get; init; }
        public string? Label { get; init; }
        public bool Usable { get; init; }
        public int? Utilization { get; init; }
    }
}
