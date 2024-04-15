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