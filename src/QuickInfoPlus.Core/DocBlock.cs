using System.Collections.Generic;
using System.Linq;

namespace QuickInfoPlus.Core
{
    public enum DocBlockKind
    {
        Text,
        Code,
    }

    public sealed class DocBlock
    {
        public DocBlock(DocBlockKind kind, string text)
            : this(kind, new[] { new DocInline(DocInlineKind.Text, text) })
        {
        }

        public DocBlock(DocBlockKind kind, IReadOnlyList<DocInline> inlines)
        {
            Kind = kind;
            Inlines = inlines;
            Text = string.Concat(inlines.Select(i => i.Text));
        }

        public DocBlockKind Kind { get; }

        public IReadOnlyList<DocInline> Inlines { get; }

        public string Text { get; }

        public override string ToString() => $"{Kind}: {Text}";
    }
}
