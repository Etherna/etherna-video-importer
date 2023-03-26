using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Extensions
{
    internal class AsyncCollectionExtensions
    {
        public static async ValueTask<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source)
        {
            var list = new List<T>();

            await foreach (var i in source)
                list.Add(i);

            return list;
        }

        public static ValueTaskAwaiter<List<T>> GetAwaiter<T>(this IAsyncEnumerable<T> source)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));

#pragma warning disable CA2012 // TODO Refactory this method
            return source.ToListAsync().GetAwaiter();
#pragma warning restore CA2012 // Use ValueTasks correctly
        }
    }
}
