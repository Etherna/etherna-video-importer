using System.IO;
using System.Text;

namespace Etherna.VideoImporter.Core.Extensions
{
    public static class StringExtensions
    {
        public static string ToSafeFileName(this string value)
        {
            var strBuilder = new StringBuilder(value);
            foreach (char c in Path.GetInvalidFileNameChars())
                strBuilder = strBuilder.Replace(c, '_');

            return strBuilder.ToString();
        }
    }
}
