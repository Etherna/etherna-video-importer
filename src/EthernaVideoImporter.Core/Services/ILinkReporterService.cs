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

using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    /// <summary>
    /// Link services
    /// </summary>
    public interface ILinkReporterService
    {
        /// <summary>
        /// Set etherna data in destination Uri.
        /// </summary>
        /// <param name="sourceVideoId">The video id on source</param>
        /// <param name="ethernaIndexId">Video Id on index</param>
        /// <param name="ethernaPermalinkHash">Video hash on permalink</param>
        Task SetEthernaReferencesAsync(
            string sourceVideoId,
            string ethernaIndexId,
            string ethernaPermalinkHash);
    }
}
