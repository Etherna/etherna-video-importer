//   Copyright 2022-present Etherna Sagl
// 
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
// 
//       http://www.apache.org/licenses/LICENSE-2.0
// 
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.

using System;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    [Flags]
    public enum SourceUriKind
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
