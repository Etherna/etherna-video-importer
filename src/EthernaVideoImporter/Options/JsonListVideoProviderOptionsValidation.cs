﻿// Copyright 2022-present Etherna SA
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

namespace Etherna.VideoImporter.Options
{
    internal sealed class JsonListVideoProviderOptionsValidation : IValidateOptions<JsonListVideoProviderOptions>
    {
        public ValidateOptionsResult Validate(string? name, JsonListVideoProviderOptions options)
        {
            if (options.JsonMetadataUri is null)
                return ValidateOptionsResult.Fail($"Json metadata uri can't be null");

            return ValidateOptionsResult.Success;
        }
    }
}
