using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis.Classification;

namespace QuickInfoPlus.Core
{
    public static class DocParser
    {
        private const char NonBreakingSpace = '\u00A0';

        // Collapses runs of whitespace except non-breaking spaces (&#160;), which keep words together.
        private static readonly Regex Whitespace = new Regex(@"[^\S\u00A0]+", RegexOptions.Compiled);

        public static IReadOnlyList<DocBlock> Parse(string? documentationCommentXml)
        {
            var document = Load(documentationCommentXml);
            if (document == null)
            {
                return Array.Empty<DocBlock>();
            }

            var examples = document.Descendants("example").Where(e => !e.Ancestors("example").Any());
            return examples.SelectMany(ParseContent).ToList();
        }

        public static IReadOnlyList<ExceptionEntry> ParseExceptions(string? documentationCommentXml)
        {
            var document = Load(documentationCommentXml);
            if (document == null)
            {
                return Array.Empty<ExceptionEntry>();
            }

            var entries = document.Descendants("exception")
                .Select(e => (Element: e, Cref: (string?)e.Attribute("cref")))
                .Where(e => !string.IsNullOrEmpty(e.Cref))
                .Select(e => new ExceptionEntry(new[] { CrefInline(e.Cref!, innerText: null) }, ParseContent(e.Element)))
                .ToList();

            return entries.Any(e => e.Description.Count > 0) ? entries : (IReadOnlyList<ExceptionEntry>)Array.Empty<ExceptionEntry>();
        }

        private static XDocument? Load(string? documentationCommentXml)
        {
            if (string.IsNullOrWhiteSpace(documentationCommentXml))
            {
                return null;
            }

            var text = "<root>" + documentationCommentXml + "</root>";
            try
            {
                var document = XDocument.Parse(text, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
                document.AddAnnotation(new SourceLines(text.Replace("\r\n", "\n").Split('\n')));
                return document;
            }
            catch (XmlException)
            {
                return null;
            }
        }

        private static string? TagLineIndentation(XElement element)
        {
            var lines = element.Document?.Annotation<SourceLines>()?.Lines;
            var lineInfo = (IXmlLineInfo)element;
            if (lines == null || !lineInfo.HasLineInfo() || lineInfo.LineNumber < 1 || lineInfo.LineNumber > lines.Length)
            {
                return null;
            }

            var line = lines[lineInfo.LineNumber - 1];
            return line.Substring(0, line.Length - line.TrimStart().Length);
        }

        private sealed class SourceLines
        {
            public SourceLines(string[] lines)
            {
                Lines = lines;
            }

            public string[] Lines { get; }
        }

        private static List<DocBlock> ParseContent(XElement element)
        {
            var blocks = new List<DocBlock>();
            var pending = new List<DocInline>();
            AppendNodes(element, pending, blocks);
            Flush(pending, blocks);
            return blocks;
        }

        internal static List<DocInline> MergeAdjacentText(IEnumerable<DocInline> inlines)
        {
            var merged = new List<DocInline>();
            foreach (var inline in inlines)
            {
                var last = merged.Count > 0 ? merged[merged.Count - 1] : null;
                if (inline.Kind == DocInlineKind.Text && last != null && last.Kind == DocInlineKind.Text && last.Classification == inline.Classification)
                {
                    merged[merged.Count - 1] = last.WithText(last.Text + inline.Text);
                }
                else
                {
                    merged.Add(inline);
                }
            }

            return merged;
        }

        private static void AppendNodes(XElement parent, List<DocInline> pending, List<DocBlock> blocks)
        {
            foreach (var node in parent.Nodes())
            {
                if (node is XText textNode)
                {
                    pending.Add(new DocInline(DocInlineKind.Text, textNode.Value));
                }
                else if (node is XElement element)
                {
                    AppendElement(element, pending, blocks);
                }
            }
        }

        private static void AppendElement(XElement element, List<DocInline> pending, List<DocBlock> blocks)
        {
            switch (element.Name.LocalName)
            {
                case "code":
                    Flush(pending, blocks);
                    var code = CodeIndentation.Normalize(element.Value, TagLineIndentation(element));
                    if (code.Length > 0)
                    {
                        blocks.Add(new DocBlock(DocBlockKind.Code, code));
                    }

                    break;
                case "para":
                    Flush(pending, blocks);
                    AppendNodes(element, pending, blocks);
                    Flush(pending, blocks);
                    break;
                case "br":
                    Flush(pending, blocks);
                    break;
                case "list":
                    AppendList(element, pending, blocks);
                    break;
                case "term":
                    AppendNodes(element, pending, blocks);
                    pending.Add(new DocInline(DocInlineKind.Text, " – "));
                    break;
                case "see":
                case "seealso":
                    AppendSee(element, pending);
                    break;
                case "paramref":
                    pending.Add(new DocInline(DocInlineKind.Text, (string?)element.Attribute("name") ?? "", classification: ClassificationTypeNames.ParameterName));
                    break;
                case "typeparamref":
                    var typeParameterName = (string?)element.Attribute("name") ?? "";
                    pending.Add(new DocInline(DocInlineKind.TypeParameterReference, typeParameterName, typeParameterName, classification: ClassificationTypeNames.TypeParameterName));
                    break;
                default:
                    AppendNodes(element, pending, blocks);
                    break;
            }
        }

        // One text block per item, marked like VS's summary: "• " for bullet lists, "1. " for numbered lists.
        private static void AppendList(XElement list, List<DocInline> pending, List<DocBlock> blocks)
        {
            Flush(pending, blocks);
            var type = (string?)list.Attribute("type");
            var number = 0;
            foreach (var child in list.Elements())
            {
                if (child.Name.LocalName == "item")
                {
                    number++;
                    var marker = type == "bullet" ? "• " : type == "number" ? $"{number}. " : "";
                    pending.Add(new DocInline(DocInlineKind.Text, marker));
                }

                AppendNodes(child, pending, blocks);
                Flush(pending, blocks);
            }
        }

        // Spaces just inside a link's custom text belong around the link, not in it.
        private static void AppendSee(XElement element, List<DocInline> pending)
        {
            var raw = element.Value;
            var hasText = !string.IsNullOrWhiteSpace(raw);
            if (hasText && IsCollapsibleSpace(raw[0]))
            {
                pending.Add(new DocInline(DocInlineKind.Text, " "));
            }

            pending.Add(SeeInline(element));

            if (hasText && IsCollapsibleSpace(raw[raw.Length - 1]))
            {
                pending.Add(new DocInline(DocInlineKind.Text, " "));
            }
        }

        private static bool IsCollapsibleSpace(char c) => char.IsWhiteSpace(c) && c != NonBreakingSpace;

        private static DocInline SeeInline(XElement element)
        {
            var innerText = string.IsNullOrWhiteSpace(element.Value) ? null : element.Value.Trim(' ', '\t', '\r', '\n');

            var href = (string?)element.Attribute("href");
            if (IsWebUrl(href))
            {
                return new DocInline(DocInlineKind.Link, innerText ?? href!, href, hasCustomText: innerText != null, hint: href);
            }

            var cref = (string?)element.Attribute("cref");
            if (!string.IsNullOrEmpty(cref))
            {
                return CrefInline(cref!, innerText);
            }

            if (innerText == null && element.Attribute("langword") is { } langword)
            {
                return new DocInline(DocInlineKind.Text, langword.Value, classification: ClassificationTypeNames.Keyword);
            }

            var text = innerText ?? href ?? "";
            return new DocInline(DocInlineKind.Text, text);
        }

        private static DocInline CrefInline(string cref, string? innerText)
        {
            var name = innerText ?? CrefFormatter.ToDisplayName(cref);
            return cref.StartsWith("!:", StringComparison.Ordinal)
                ? new DocInline(DocInlineKind.Text, name)
                : new DocInline(DocInlineKind.SymbolLink, name, cref, hasCustomText: innerText != null);
        }

        private static bool IsWebUrl(string? href)
        {
            return href != null
                && Uri.TryCreate(href, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private static void Flush(List<DocInline> pending, List<DocBlock> blocks)
        {
            var kept = new List<DocInline>();
            foreach (var inline in pending)
            {
                var text = Whitespace.Replace(inline.Text, " ");
                if (kept.Count > 0 && kept[kept.Count - 1].Text.EndsWith(" ", StringComparison.Ordinal) && text.StartsWith(" ", StringComparison.Ordinal))
                {
                    text = text.Substring(1);
                }

                if (text.Length > 0)
                {
                    kept.Add(inline.WithText(text));
                }
            }

            pending.Clear();

            while (kept.Count > 0)
            {
                var first = kept[0];
                var trimmed = first.Text.TrimStart(' ');
                if (trimmed.Length > 0)
                {
                    kept[0] = first.WithText(trimmed);
                    break;
                }

                kept.RemoveAt(0);
            }

            while (kept.Count > 0)
            {
                var last = kept[kept.Count - 1];
                var trimmed = last.Text.TrimEnd(' ');
                if (trimmed.Length > 0)
                {
                    kept[kept.Count - 1] = last.WithText(trimmed);
                    break;
                }

                kept.RemoveAt(kept.Count - 1);
            }

            if (kept.Count > 0)
            {
                blocks.Add(new DocBlock(DocBlockKind.Text, MergeAdjacentText(kept)));
            }
        }
    }
}
