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
using System.Globalization;

namespace Etherna.VideoImporter.Core.Services
{
    public class ConsoleIoService : IIoService
    {
        // Consts.
        private const ConsoleColor ErrorForegroundColor = ConsoleColor.DarkRed;
        private const ConsoleColor SuccessForegroundColor = ConsoleColor.DarkGreen;
        
        // Events.
        public event ConsoleCancelEventHandler? CancelKeyPress 
        {
            add => Console.CancelKeyPress += value;
            remove => Console.CancelKeyPress -= value;
        }

        // Properties.
        public int BufferWidth => Console.BufferWidth;
        public int CursorTop
        {
            get => Console.CursorTop;
            set => Console.CursorTop = value;
        }

        // Methods.
        public void PrintException(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception, nameof(exception));
            WriteLine($"{exception.GetType().Name}: {exception.Message}", false);
        }

        public void PrintTimeStamp() => Console.Write($"[{GetTimeStamp()}] ");

        public ConsoleKeyInfo ReadKey() => Console.ReadKey();

        public string? ReadLine() => Console.ReadLine();
        
        public void SetCursorPosition(int left, int top) => Console.SetCursorPosition(left, top);

        public void Write(string? value) => Console.Write(value);
        
        public void WriteError(string value)
        {
            Console.ForegroundColor = ErrorForegroundColor;
            Console.Write(value);
            Console.ResetColor();
        }

        public void WriteErrorLine(string value, bool addTimeStamp = true)
        {
            Console.ForegroundColor = ErrorForegroundColor;
            Console.WriteLine(addTimeStamp ? GetLineWithTimeStamp(value) : value);
            Console.ResetColor();
        }

        public void WriteLine(string? value = null, bool addTimeStamp = true) =>
            Console.WriteLine(addTimeStamp ? GetLineWithTimeStamp(value) : value);
        
        public void WriteSuccess(string value)
        {
            Console.ForegroundColor = SuccessForegroundColor;
            Console.Write(value);
            Console.ResetColor();
        }

        public void WriteSuccessLine(string value, bool addTimeStamp = true)
        {
            Console.ForegroundColor = SuccessForegroundColor;
            Console.WriteLine(addTimeStamp ? GetLineWithTimeStamp(value) : value);
            Console.ResetColor();
        }
        
        // Helpers.
        private static string GetTimeStamp() =>
            DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

        private static string GetLineWithTimeStamp(string? value) =>
            $"[{GetTimeStamp()}] {value ?? ""}";
    }
}