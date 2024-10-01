// Copyright 2022-present Etherna SA
// This file is part of Etherna Video Importer.
// 
// Etherna Video Importer is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// Etherna Video Importer is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with Etherna Video Importer.
// If not, see <https://www.gnu.org/licenses/>.

using System;

namespace Etherna.VideoImporter.Core.Services
{
    public interface IIoService
    {
        // Events.
        event ConsoleCancelEventHandler? CancelKeyPress;
        
        // Properties.
        int BufferWidth { get; }
        int CursorTop { get; set; }

        // Methods.
        void PrintException(Exception exception);
        void PrintTimeStamp();
        ConsoleKeyInfo ReadKey();
        string? ReadLine();
        void SetCursorPosition(int left, int top);
        void Write(string? value);
        void WriteError(string value);
        void WriteErrorLine(string value, bool addTimeStamp = true);
        void WriteLine(string? value = null, bool addTimeStamp = true);
        void WriteSuccess(string value);
        void WriteSuccessLine(string value, bool addTimeStamp = true);
    }
}