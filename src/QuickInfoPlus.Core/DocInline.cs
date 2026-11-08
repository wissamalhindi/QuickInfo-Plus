using Microsoft.CodeAnalysis.Classification;

namespace QuickInfoPlus.Core
{
    public enum DocInlineKind
    {
        Text,
        Link,
        SymbolLink,
        TypeParameterReference,
    }

    public sealed class DocInline
    {
        public DocInline(
            DocInlineKind kind,
            string text,
            string? target = null,
            bool hasCustomText = false,
            string? classification = null,
            string? hint = null)
        {
            Kind = kind;
            Text = text;
            Target = target;
            HasCustomText = hasCustomText;
            Classification = classification ?? ClassificationTypeNames.Text;
            Hint = hint;
        }

        public DocInlineKind Kind { get; }

        public string Text { get; }

        public string? Target { get; }

        public bool HasCustomText { get; }

        // Roslyn classification type name (ClassificationTypeNames), used to color the text like VS's summary.
        public string Classification { get; }

        // Shown when hovering a link, like VS's summary: the referenced symbol's name or the URL.
        public string? Hint { get; }

        public DocInline WithText(string text) => new DocInline(Kind, text, Target, HasCustomText, Classification, Hint);

        public override string ToString() => Target == null ? $"{Kind}: {Text}" : $"{Kind}: {Text} -> {Target}";
    }
}
