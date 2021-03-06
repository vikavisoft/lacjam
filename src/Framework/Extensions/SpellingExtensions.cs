﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Lacjam.Framework.Extensions
{
    public static class SpellingExtensions
    {
        private const string Alphabet = "abcdefghijklmnopqrstuvwxyz";

        public static IEnumerable<string> SpellingVariants(this string word, SpellingVariants spellingVariants)
        {
            switch (spellingVariants)
            {
                case Framework.Extensions.SpellingVariants.None:
                    return new[] { word };
                case Framework.Extensions.SpellingVariants.One:
                    return Edits(word);
                case Framework.Extensions.SpellingVariants.Two:
                    return Edits(word).SelectMany(Edits).ToArray();
            }
            return null;
        }

        private static IEnumerable<string> Edits(string word)
        {
            var set = new HashSet<string>();

            var splits = Splits(set, word);
            set.AddRange(Deletes(splits));
            set.AddRange(Transposes(splits));
            set.AddRange(Replaces(splits));
            set.AddRange(Inserts(splits));

            return set;
        }

        private static IEnumerable<Tuple<string,string>> Splits(HashSet<string> set, string word)
        {
            var splits = new List<Tuple<string, string>>();
            for (int i = 0; i < word.Length; i++)
            {
                splits.Add(new Tuple<string, string>(word.Substring(0,i),word.Substring(i,word.Length-i)));
            }
            return splits;
        }

        private static IEnumerable<string> Deletes(IEnumerable<Tuple<string, string>> splits)
        {
            return splits
                .Where(x => x.Item2.Length > 0)
                .Select(x => x.Item1 + x.Item2.Substring(1));
        }

        private static IEnumerable<string> Transposes(IEnumerable<Tuple<string, string>> splits)
        {
            return splits
                .Where(x => x.Item2.Length > 1)
                .Select(x => x.Item1 + x.Item2[1]+x.Item2[0]+x.Item2.Substring(2));
        }

        private static IEnumerable<string> Replaces(IEnumerable<Tuple<string, string>> splits)
        {
            return Alphabet.SelectMany(x => Replaces(splits, x));
        }
        private static IEnumerable<string> Replaces(IEnumerable<Tuple<string, string>> splits, char letter)
        {
            return splits
                .Where(x => x.Item2.Length > 0)
                .Select(x => x.Item1 + letter + x.Item2.Substring(1));
        }

        private static IEnumerable<string> Inserts(IEnumerable<Tuple<string, string>> splits)
        {
            return Alphabet.SelectMany(x => Inserts(splits, x));
        }
        private static IEnumerable<string> Inserts(IEnumerable<Tuple<string, string>> splits, char letter)
        {
            return splits
                .Select(x => x.Item1 + letter + x.Item2);
        }
    }
    public static class SetExtensions
    {
        public static void AddRange(this ISet<string> set, IEnumerable<string> other )
        {
            foreach (var element in other)
            {
                if (!string.IsNullOrWhiteSpace(element))
                    set.Add(element);
            }
        }
    }
    public enum SpellingVariants
    {
        None = 0,
        One = 1,
        Two = 2
    }
}