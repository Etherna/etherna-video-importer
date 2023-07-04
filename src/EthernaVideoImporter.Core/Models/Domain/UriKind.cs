using System;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    [Flags]
    public enum UriKind
    {
        None = 0,
        LocalAbsolute = 1,
        LocalRelative = 2,
        OnlineAbsolute = 4,
        OnlineRelative = 8,
        Absolute = LocalAbsolute | OnlineAbsolute,
        Relative = LocalRelative | OnlineRelative,
        Local = LocalAbsolute | LocalRelative,
        Online = OnlineAbsolute | OnlineRelative,
        All = Absolute | Relative,
    }
}
