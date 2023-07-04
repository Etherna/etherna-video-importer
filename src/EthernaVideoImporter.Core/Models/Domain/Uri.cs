using System;
using System.IO;
using System.Linq;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public class Uri
    {
        // Constructors.
        public Uri(
            string uri,
            UriKind allowedUriKinds = UriKind.All)
        {
            if (string.IsNullOrEmpty(uri))
                throw new ArgumentException("Uri cannot be null or empty", nameof(uri));

            OriginalUri = uri;
            UriKind = GetUriKind(uri, allowedUriKinds);

            // Final check.
            if (UriKind == UriKind.None)
                throw new ArgumentException("Invalid uri with allowed uri types", nameof(uri));
        }

        // Properties.
        public string OriginalUri { get; }
        public UriKind UriKind { get; }

        // Methods.
        /// <summary>
        /// Get current uri as an absolute uri
        /// </summary>
        /// <param name="allowedUriKinds">Optional restrictions for original uri kind</param>
        /// <param name="baseDirectory">Optional base directory, required for online relative uri</param>
        /// <returns>Absolute uri and its kind</returns>
        public (string, UriKind) ToAbsoluteUri(UriKind allowedUriKinds = UriKind.All, string? baseDirectory = null)
        {
            // Define actual allowed uri kinds.
            var actualAllowedUriKinds = allowedUriKinds & UriKind;

            if (baseDirectory is not null)
            {
                var baseDirectoryUriKind = GetUriKind(baseDirectory, UriKind.Absolute);
                if (baseDirectoryUriKind == UriKind.None)
                    throw new ArgumentException("Base directory is not a valid absolute uri", nameof(baseDirectory));

                if (baseDirectoryUriKind == UriKind.LocalAbsolute)
                    actualAllowedUriKinds &= UriKind.Local;
                else if (baseDirectoryUriKind == UriKind.OnlineAbsolute)
                    actualAllowedUriKinds &= UriKind.Online;
                else throw new InvalidOperationException("Base directory can only be absolute");
            }

            // Checks.
            //if could be an online relative uri, and base directory is null. If positive, in any case uri can't be absolute
            if ((actualAllowedUriKinds | UriKind.OnlineRelative) != 0 &&
                baseDirectory is null)
            {
                if ((actualAllowedUriKinds | UriKind.LocalRelative) != 0)
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
                UriKind.LocalAbsolute => (OriginalUri, UriKind.LocalAbsolute),
                UriKind.LocalRelative => (Path.GetFullPath(OriginalUri, baseDirectory ?? Directory.GetCurrentDirectory()), UriKind.LocalAbsolute),
                UriKind.OnlineAbsolute => (OriginalUri, UriKind.OnlineAbsolute),
                UriKind.OnlineRelative => (new System.Uri(new System.Uri(baseDirectory!, System.UriKind.Absolute), OriginalUri).ToString(),
                                           UriKind.OnlineAbsolute),
                _ => throw new InvalidOperationException("Invalid uri kind. It should be well defined at this stage")
            };
        }

        /// <summary>
        /// Get parent directory as an absolute uri
        /// </summary>
        /// <param name="allowedUriKinds">Optional restrictions for original uri kind</param>
        /// <param name="baseDirectory">Optional base directory, required for online relative uri</param>
        /// <returns>Parent directory absolute uri and its kind</returns>
        public (string, UriKind)? TryGetParentDirectoryAsAbsoluteUri(UriKind allowedUriKinds = UriKind.All, string? baseDirectory = null)
        {
            var (absoluteUri, absoluteUriKind) = ToAbsoluteUri(allowedUriKinds, baseDirectory);

            switch (absoluteUriKind)
            {
                case UriKind.LocalAbsolute:
                    var dirName = Path.GetDirectoryName(absoluteUri);
                    return dirName is null ? null :
                        (dirName, UriKind.LocalAbsolute);

                case UriKind.OnlineAbsolute:
                    var segments = new System.Uri(absoluteUri, System.UriKind.Absolute).Segments;
                    return segments.Length == 1 ? null : //if it's already root, return null
                        (absoluteUri[..^segments.Last().Length], UriKind.OnlineAbsolute);

                default: throw new InvalidOperationException("Invalid absolute uri kind. It should be well defined and absolute");
            }
        }

        // Public static methods.
        public static UriKind GetUriKind(string uri, UriKind allowedUriKinds)
        {
            var uriKind = UriKind.None;

            //test local
            if (!uri.Intersect(Path.GetInvalidPathChars()).Any())
            {
                var isRooted = Path.IsPathRooted(uri);

                //absolute
                if (isRooted && (allowedUriKinds & UriKind.LocalAbsolute) != 0)
                    uriKind |= UriKind.LocalAbsolute;

                //relative
                if (!isRooted && (allowedUriKinds & UriKind.LocalRelative) != 0)
                    uriKind |= UriKind.LocalRelative;
            }

            //test online absolute
            if (System.Uri.TryCreate(uri, System.UriKind.Absolute, out var onlineAbsUriResult) &&
                (onlineAbsUriResult.Scheme == System.Uri.UriSchemeHttp || onlineAbsUriResult.Scheme == System.Uri.UriSchemeHttps) &&
                (allowedUriKinds & UriKind.OnlineAbsolute) != 0)
                uriKind |= UriKind.OnlineAbsolute;

            //test online relative
            if (System.Uri.TryCreate(uri, System.UriKind.Relative, out var _) &&
                (allowedUriKinds & UriKind.OnlineRelative) != 0)
                uriKind |= UriKind.OnlineRelative;

            return uriKind;
        }
    }
}
