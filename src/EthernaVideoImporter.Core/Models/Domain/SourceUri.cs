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
            if (string.IsNullOrWhiteSpace(uri))
                throw new ArgumentException("Uri cannot be null or white spaces", nameof(uri));

            this.defaultBaseDirectory = defaultBaseDirectory;
            OriginalUri = uri;
            UriKind = GetUriKind(uri) & allowedUriKinds;

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
            var actualAllowedUriKinds = UriKind & allowedUriKinds;

            // Check with base directory.
            baseDirectory ??= defaultBaseDirectory;
            if ((actualAllowedUriKinds & SourceUriKind.Relative) != 0 &&
                baseDirectory is not null)
            {
                var baseDirectoryUriKind = GetUriKind(baseDirectory) & SourceUriKind.Absolute;

                actualAllowedUriKinds &= baseDirectoryUriKind switch
                {
                    SourceUriKind.LocalAbsolute => SourceUriKind.Local,
                    SourceUriKind.OnlineAbsolute => SourceUriKind.Online,
                    _ => throw new InvalidOperationException("Base directory can only be absolute"),
                };
            }

            // Checks.
            //local and online ambiguity
            if ((actualAllowedUriKinds & SourceUriKind.Local) != 0 &&
                (actualAllowedUriKinds & SourceUriKind.Online) != 0)
                throw new InvalidOperationException("Unable to distinguish between local and online uri. Try to restrict allowed uri kinds");

            //check if could be an online relative uri, and base directory is null
            if ((actualAllowedUriKinds & SourceUriKind.OnlineRelative) != 0 &&
                baseDirectory is null)
                throw new InvalidOperationException("Can't resolve online relative uri. Specify a base directory");

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
                SourceUriKind.OnlineRelative => (new Uri(new Uri(baseDirectory!, System.UriKind.Absolute), OriginalUri).ToString(),
                                           SourceUriKind.OnlineAbsolute),
                _ => throw new InvalidOperationException("Can't find a valid uri kind")
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
            baseDirectory ??= defaultBaseDirectory;
            var (absoluteUri, absoluteUriKind) = ToAbsoluteUri(allowedUriKinds, baseDirectory);

            switch (absoluteUriKind)
            {
                case SourceUriKind.LocalAbsolute:
                    var dirName = Path.GetDirectoryName(absoluteUri);
                    return dirName is null ? null :
                        (dirName, SourceUriKind.LocalAbsolute);

                case SourceUriKind.OnlineAbsolute:
                    var segments = new Uri(absoluteUri, System.UriKind.Absolute).Segments;
                    return segments.Length == 1 ? null : //if it's already root, return null
                        (absoluteUri[..^segments.Last().Length], SourceUriKind.OnlineAbsolute);

                default: throw new InvalidOperationException("Invalid absolute uri kind. It should be well defined and absolute");
            }
        }

        // Public static methods.
        public static SourceUri FromString(string uri) => new(uri);

        /// <summary>
        /// Try to identify the uri kind, doesn't validate local paths. Online absolute paths can't be local
        /// </summary>
        /// <param name="uri">The input uti</param>
        /// <returns>Identified uri kind</returns>
        public static SourceUriKind GetUriKind(string uri)
        {
            if (uri is null)
                throw new ArgumentNullException(nameof(uri));

            var uriKind = SourceUriKind.None;

            if (uri.Length > 0)
            {
                //test online absolute
                if (Uri.TryCreate(uri, System.UriKind.Absolute, out var onlineAbsUriResult) &&
                (onlineAbsUriResult.Scheme == Uri.UriSchemeHttp || onlineAbsUriResult.Scheme == Uri.UriSchemeHttps))
                    uriKind |= SourceUriKind.OnlineAbsolute;

                //test online relative
                if (Uri.TryCreate(uri, System.UriKind.Relative, out var _))
                    uriKind |= SourceUriKind.OnlineRelative;

                //test local absolute and relative
                if ((uriKind & SourceUriKind.OnlineAbsolute) == 0)
                {
                    uriKind |= Path.IsPathRooted(uri) ?
                        SourceUriKind.LocalAbsolute :
                        SourceUriKind.LocalRelative;
                }
            }

            return uriKind;
        }

        // Operators.
        public static implicit operator SourceUri(string uri) => FromString(uri);
    }
}
