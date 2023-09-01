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

namespace Etherna.VideoImporter.Core.Models.ModelView
{
    public class ImportSummaryModelView
    {
        // Properties.
        public int TotDeletedExogenous { get; set; }
        public int TotDeletedRemovedFromSource { get; set; }
        public int TotErrorVideoImported { get; set; }
        public int TotProcessedVideos => TotSuccessVideoImported + TotUpdatedVideoImported + TotSkippedVideoImported + TotErrorVideoImported;
        public int TotSkippedVideoImported { get; set; }
        public int TotSuccessVideoImported { get; set; }
        public int TotUpdatedVideoImported { get; set; }
    }
}
