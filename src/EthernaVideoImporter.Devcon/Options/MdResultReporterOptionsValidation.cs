// Copyright 2022-present Etherna SA
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Microsoft.Extensions.Options;
using System.IO;

namespace Etherna.VideoImporter.Devcon.Options
{
    internal sealed class MdResultReporterOptionsValidation : IValidateOptions<MdResultReporterOptions>
    {
        public ValidateOptionsResult Validate(string? name, MdResultReporterOptions options)
        {
            if (!Directory.Exists(options.MdResultFolderPath))
                return ValidateOptionsResult.Fail($"Not found MD directory path at ({options.MdResultFolderPath})");

            return ValidateOptionsResult.Success;
        }
    }
}