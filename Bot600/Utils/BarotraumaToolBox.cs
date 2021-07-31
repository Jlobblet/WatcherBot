using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading.Tasks;
using Discord;

namespace Bot600.Utils
{
    public static class BarotraumaToolBox
    {
        internal static async Task InternalLog(LogMessage msg)
        {
            await Task.Yield();

            Console.WriteLine($"[{msg.Severity}] {msg.Source}: {msg.Message}");
            if (msg.Exception is not null)
            {
                Console.WriteLine($"Exception: {msg.Exception.Message} {msg.Exception.StackTrace}");
            }
        }

        public static int CountSubstrings(this string str, string substr)
        {
            var count = 0;
            var index = 0;
            while (true)
            {
                index = str.IndexOf(substr, index, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    break;
                }

                index++;
                count++;
            }

            return count;
        }

        public static bool ToBool(this IsCringe cringe) => cringe == IsCringe.Yes;

        public static IsCringe ToCringe(this bool @bool) => @bool ? IsCringe.Yes : IsCringe.No;

        [return: NotNull]
        [Pure]
        public static IEnumerable<TResult> WhereSelect<TSource, TResult>(
            [NotNull] this IEnumerable<TSource> source,
            [NotNull] Func<TSource, bool> predicate,
            [NotNull] Func<TSource, TResult> mapping) =>
            source.Where(predicate).Select(mapping);

        [return: NotNull]
        [Pure]
        public static IEnumerable<TSource> FilterZip<TSource>(
            [NotNull] this IEnumerable<TSource> source,
            [NotNull] IEnumerable<bool> filter) =>
            source.Zip(filter).WhereSelect(tup => tup.Second, tup => tup.First);
    }
}
