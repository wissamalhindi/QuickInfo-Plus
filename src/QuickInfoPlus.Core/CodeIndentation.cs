using System;
using System.Collections.Generic;
using System.Linq;

namespace QuickInfoPlus.Core
{
    public static class CodeIndentation
    {
        public static string Normalize(string code) => Normalize(code, tagLineIndentation: null);

        // tagLineIndentation: leading whitespace of the doc-comment line holding the <code> tag. When the code starts on
        // that same line, the first line carries no indentation, so the following lines are measured against it instead.
        public static string Normalize(string code, string? tagLineIndentation)
        {
            var lines = code.Replace("\r\n", "\n").Split('\n').Select(l => l.TrimEnd()).ToList();
            if (tagLineIndentation != null && lines.Count > 0 && lines[0].Trim().Length > 0)
            {
                var rest = lines.Skip(1)
                    .Select(l => l.Length == 0 ? l : l.Substring(SharedPrefixLength(Indentation(l), tagLineIndentation)))
                    .ToList();
                while (rest.Count > 0 && rest[rest.Count - 1].Length == 0)
                {
                    rest.RemoveAt(rest.Count - 1);
                }

                return string.Join("\n", new[] { lines[0].Trim() }.Concat(rest));
            }

            return RemoveSharedIndentation(lines);
        }

        private static string RemoveSharedIndentation(List<string> lines)
        {
            while (lines.Count > 0 && lines[0].Length == 0)
            {
                lines.RemoveAt(0);
            }

            while (lines.Count > 0 && lines[lines.Count - 1].Length == 0)
            {
                lines.RemoveAt(lines.Count - 1);
            }

            if (lines.Count == 0)
            {
                return "";
            }

            var prefix = SharedIndentation(lines.Where(l => l.Length > 0));
            return string.Join("\n", lines.Select(l => l.Length == 0 ? l : l.Substring(prefix.Length)));
        }

        private static string SharedIndentation(IEnumerable<string> lines)
        {
            string? prefix = null;
            foreach (var line in lines)
            {
                var indentation = Indentation(line);
                if (prefix == null)
                {
                    prefix = indentation;
                    continue;
                }

                prefix = prefix.Substring(0, SharedPrefixLength(prefix, indentation));
            }

            return prefix ?? "";
        }

        private static string Indentation(string line) => line.Substring(0, line.Length - line.TrimStart().Length);

        private static int SharedPrefixLength(string a, string b)
        {
            var length = 0;
            var max = Math.Min(a.Length, b.Length);
            while (length < max && a[length] == b[length])
            {
                length++;
            }

            return length;
        }
    }
}
