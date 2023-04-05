using System;

namespace Etherna.VideoImporter.Core.Extensions
{
    public static class KeccakExtensions
    {
        public static string ToHash(this byte[] value)
        {
            string hashString = BitConverter.ToString(value);
            hashString = hashString.Replace("-", "", StringComparison.InvariantCulture).ToUpperInvariant();
            return hashString;
        }
    }
}
