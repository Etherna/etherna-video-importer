using System;
using System.IO;
using System.Linq;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public class SourceUri
    {
        // Fields.
        private readonly string? defaultBaseDirectory;

        // Constructor.
        public SourceUri(
            string uri,
            SourceUriKind allowedUriKinds = SourceUriKind.All,
            string? defaultBaseDirectory = null)
        {
            if (string.IsNullOrEmpty(uri))
                throw new ArgumentException("Uri cannot be null or empty", nameof(uri));

            this.defaultBaseDirectory = defaultBaseDirectory;
            OriginalUri = uri;
            UriKind = GetUriKind(uri, allowedUriKinds);

            // Final check.
            if (UriKind == SourceUriKind.None)
                throw new ArgumentException("Invalid uri with allowed uri types", nameof(uri));
        }

        // Properties.
        public string OriginalUri { get; }
        public SourceUriKind UriKind { get; }

        // Methods.
        /// <summary>
        /// Get current uri as an absolute uri
        /// </summary>
        /// <param name="allowedUriKinds">Optional restrictions for original uri kind</param>
        /// <param name="baseDirectory">Optional base directory, required for online relative uri</param>
        /// <returns>Absolute uri and its kind</returns>
        public (string, SourceUriKind) ToAbsoluteUri(SourceUriKind allowedUriKinds = SourceUriKind.All, string? baseDirectory = null)
        {
            // Define actual allowed uri kinds.
            var actualAllowedUriKinds = allowedUriKinds & UriKind;

            baseDirectory ??= defaultBaseDirectory;
            if (baseDirectory is not null)
            {
                var baseDirectoryUriKind = GetUriKind(baseDirectory, SourceUriKind.Absolute);
                if (baseDirectoryUriKind == SourceUriKind.None)
                    throw new ArgumentException("Base directory is not a valid absolute uri", nameof(baseDirectory));

                if (baseDirectoryUriKind == SourceUriKind.LocalAbsolute)
                    actualAllowedUriKinds &= SourceUriKind.Local;
                else if (baseDirectoryUriKind == SourceUriKind.OnlineAbsolute)
                    actualAllowedUriKinds &= SourceUriKind.Online;
                else throw new InvalidOperationException("Base directory can only be absolute");
            }

            // Checks.
            //if could be an online relative uri, and base directory is null. If positive, in any case uri can't be absolute
            if ((actualAllowedUriKinds | SourceUriKind.OnlineRelative) != 0 &&
                baseDirectory is null)
            {
                if ((actualAllowedUriKinds | SourceUriKind.LocalRelative) != 0)
                    throw new InvalidOperationException("Can't resolve undefined relative uri. Specify if is local, or a base directory");
                else
                    throw new InvalidOperationException("Can't resolve online relative uri. Specify a base directory");
            }

            // Resolve.
            /*
             * At this point we know what the exact kind is:
             * - if can be local absolute, then it can't be a relative or an online uri.
             * - if can be online absolute, then it can't be a relative or a local uri.
             * - if can be local relative, then it can't be an absolute or an online relative.
             *   This because if online relative was an option, it already verified presence of a base directory.
             *   And if base directory is present and valid, it already defined if uri is local or online.
             * - if can be online relative, then it can't be an absoulute or a local relative.
             *   It implies that a base directory must be present, and this implies same previus considerations.
             */
            return actualAllowedUriKinds switch
            {
                SourceUriKind.LocalAbsolute => (OriginalUri, SourceUriKind.LocalAbsolute),
                SourceUriKind.LocalRelative => (Path.GetFullPath(OriginalUri, baseDirectory ?? Directory.GetCurrentDirectory()), SourceUriKind.LocalAbsolute),
                SourceUriKind.OnlineAbsolute => (OriginalUri, SourceUriKind.OnlineAbsolute),
                SourceUriKind.OnlineRelative => (new System.Uri(new System.Uri(baseDirectory!, System.UriKind.Absolute), OriginalUri).ToString(),
                                           SourceUriKind.OnlineAbsolute),
                _ => throw new InvalidOperationException("Invalid uri kind. It should be well defined at this stage")
            };
        }

        /// <summary>
        /// Get parent directory as an absolute uri
        /// </summary>
        /// <param name="allowedUriKinds">Optional restrictions for original uri kind</param>
        /// <param name="baseDirectory">Optional base directory, required for online relative uri</param>
        /// <returns>Parent directory absolute uri and its kind</returns>
        public (string, SourceUriKind)? TryGetParentDirectoryAsAbsoluteUri(SourceUriKind allowedUriKinds = SourceUriKind.All, string? baseDirectory = null)
        {
            var (absoluteUri, absoluteUriKind) = ToAbsoluteUri(allowedUriKinds, baseDirectory);

            switch (absoluteUriKind)
            {
                case SourceUriKind.LocalAbsolute:
                    var dirName = Path.GetDirectoryName(absoluteUri);
                    return dirName is null ? null :
                        (dirName, SourceUriKind.LocalAbsolute);

                case SourceUriKind.OnlineAbsolute:
                    var segments = new System.Uri(absoluteUri, System.UriKind.Absolute).Segments;
                    return segments.Length == 1 ? null : //if it's already root, return null
                        (absoluteUri[..^segments.Last().Length], SourceUriKind.OnlineAbsolute);

                default: throw new InvalidOperationException("Invalid absolute uri kind. It should be well defined and absolute");
            }
        }

        // Public static methods.
        public static SourceUri FromString(string uri) => new(uri);

        public static SourceUriKind GetUriKind(string uri, SourceUriKind allowedUriKinds)
        {
            var uriKind = SourceUriKind.None;

            //test local
            if (!uri.Intersect(Path.GetInvalidPathChars()).Any())
            {
                var isRooted = Path.IsPathRooted(uri);

                //absolute
                if (isRooted && (allowedUriKinds & SourceUriKind.LocalAbsolute) != 0)
                    uriKind |= SourceUriKind.LocalAbsolute;

                //relative
                if (!isRooted && (allowedUriKinds & SourceUriKind.LocalRelative) != 0)
                    uriKind |= SourceUriKind.LocalRelative;
            }

            //test online absolute
            if (System.Uri.TryCreate(uri, System.UriKind.Absolute, out var onlineAbsUriResult) &&
                (onlineAbsUriResult.Scheme == System.Uri.UriSchemeHttp || onlineAbsUriResult.Scheme == System.Uri.UriSchemeHttps) &&
                (allowedUriKinds & SourceUriKind.OnlineAbsolute) != 0)
                uriKind |= SourceUriKind.OnlineAbsolute;

            //test online relative
            if (System.Uri.TryCreate(uri, System.UriKind.Relative, out var _) &&
                (allowedUriKinds & SourceUriKind.OnlineRelative) != 0)
                uriKind |= SourceUriKind.OnlineRelative;

            return uriKind;
        }

        // Operators.
        public static implicit operator SourceUri(string uri) => FromString(uri);
    }
}
