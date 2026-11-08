using System.Collections.Generic;
using System.Linq;
using QuickInfoPlus.Core;
using Xunit;

namespace QuickInfoPlus.Core.Tests
{
    public class ExceptionParserTests
    {
        private static string Doc(string body) => "<member name=\"M:X\">" + body + "</member>";

        private static (DocInlineKind, string, string?)[] Inlines(IEnumerable<DocInline> inlines) =>
            inlines.Select(i => (i.Kind, i.Text, i.Target)).ToArray();

        private static (DocBlockKind, string)[] Blocks(IEnumerable<DocBlock> blocks) =>
            blocks.Select(b => (b.Kind, b.Text)).ToArray();

        [Fact]
        public void ParseExceptions_TypeAndDescription()
        {
            var entries = DocParser.ParseExceptions(Doc(
                "<exception cref=\"T:System.ArgumentNullException\"><paramref name=\"src\"/> is null.</exception>"));

            var entry = Assert.Single(entries);
            Assert.Equal(
                new[] { (DocInlineKind.SymbolLink, "ArgumentNullException", (string?)"T:System.ArgumentNullException") },
                Inlines(entry.Type));
            Assert.False(entry.Type[0].HasCustomText);
            Assert.Equal(new[] { (DocBlockKind.Text, "src is null.") }, Blocks(entry.Description));
        }

        [Fact]
        public void ParseExceptions_DocumentOrder_EmptyDescriptionKept()
        {
            var entries = DocParser.ParseExceptions(Doc(
                "<exception cref=\"T:A\">first</exception><summary>S</summary><exception cref=\"T:B\"/>"));

            Assert.Equal(new[] { "A", "B" }, entries.Select(e => e.Type.Single().Text));
            Assert.Equal(new[] { (DocBlockKind.Text, "first") }, Blocks(entries[0].Description));
            Assert.Empty(entries[1].Description);
        }

        [Fact]
        public void ParseExceptions_NoDescriptions_ReturnsEmpty()
        {
            Assert.Empty(DocParser.ParseExceptions(Doc("<exception cref=\"T:A\"/><exception cref=\"T:B\">  </exception>")));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("<member name=\"M:X\"><summary>S</summary></member>")]
        public void ParseExceptions_None_ReturnsEmpty(string? xml)
        {
            Assert.Empty(DocParser.ParseExceptions(xml));
        }

        [Fact]
        public void ParseExceptions_WithoutCref_Skipped()
        {
            var entries = DocParser.ParseExceptions(Doc("<exception>orphan</exception><exception cref=\"T:A\">kept</exception>"));

            Assert.Equal("A", Assert.Single(entries).Type.Single().Text);
        }

        [Fact]
        public void ParseExceptions_UnresolvedCref_IsText()
        {
            var entry = Assert.Single(DocParser.ParseExceptions(Doc("<exception cref=\"!:Bad\">broken</exception>")));

            Assert.Equal(new[] { (DocInlineKind.Text, "Bad", (string?)null) }, Inlines(entry.Type));
        }

        [Fact]
        public void ParseExceptions_MultiParagraph()
        {
            var entry = Assert.Single(DocParser.ParseExceptions(Doc(
                "<exception cref=\"T:E\"><para>One.</para><code>x();</code></exception>")));

            Assert.Equal(new[] { (DocBlockKind.Text, "One."), (DocBlockKind.Code, "x();") }, Blocks(entry.Description));
        }

        [Fact]
        public void ParseExceptions_Malformed_ReturnsEmpty()
        {
            Assert.Empty(DocParser.ParseExceptions("<member><exception cref=\"T:E\">a < b</exception></member>"));
        }
    }
}
