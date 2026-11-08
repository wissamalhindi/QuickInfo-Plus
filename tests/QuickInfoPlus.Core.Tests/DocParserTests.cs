using System.Collections.Generic;
using System.Linq;
using QuickInfoPlus.Core;
using Xunit;

namespace QuickInfoPlus.Core.Tests
{
    public class DocParserTests
    {
        private static string Doc(string body) => "<member name=\"M:X\">" + body + "</member>";

        private static (DocBlockKind, string)[] Blocks(IReadOnlyList<DocBlock> blocks) =>
            blocks.Select(b => (b.Kind, b.Text)).ToArray();

        private static (DocBlockKind, string) Text(string text) => (DocBlockKind.Text, text);

        private static (DocBlockKind, string) Code(string text) => (DocBlockKind.Code, text);

        private static (DocInlineKind, string, string?)[] Inlines(DocBlock block) =>
            block.Inlines.Select(i => (i.Kind, i.Text, i.Target)).ToArray();

        private static (DocInlineKind, string, string?) TextInline(string text) => (DocInlineKind.Text, text, null);

        private static DocBlock SingleBlock(string body)
        {
            var blocks = DocParser.Parse(Doc(body));
            Assert.Single(blocks);
            return blocks[0];
        }

        [Fact]
        public void Parse_CrefWithoutText_NotCustom()
        {
            var block = SingleBlock("<example><see cref=\"T:Ns.F\"/></example>");

            Assert.False(block.Inlines.Single().HasCustomText);
        }

        [Fact]
        public void Parse_CrefWithText_Custom()
        {
            var block = SingleBlock("<example><see cref=\"T:Ns.F\">helper</see></example>");

            Assert.True(block.Inlines.Single().HasCustomText);
        }

        [Fact]
        public void Parse_TypeParamRef_IsTypeParameterReference()
        {
            var block = SingleBlock("<example><typeparamref name=\"T\"/></example>");

            Assert.Equal(new[] { (DocInlineKind.TypeParameterReference, "T", (string?)"T") }, Inlines(block));
        }

        [Fact]
        public void Parse_CustomLinkText_SpacesMovedOutsideLink()
        {
            var block = SingleBlock("<example>See <see cref=\"T:Ns.F\"> the helper </see> now.</example>");

            Assert.Equal(
                new[] { TextInline("See "), (DocInlineKind.SymbolLink, "the helper", (string?)"T:Ns.F"), TextInline(" now.") },
                Inlines(block));
        }

        [Fact]
        public void Parse_CustomLinkText_SpacesKeptAsWordSeparators()
        {
            var block = SingleBlock("<example>See<see href=\"https://x.test\"> docs </see>now.</example>");

            Assert.Equal(
                new[] { TextInline("See "), (DocInlineKind.Link, "docs", (string?)"https://x.test"), TextInline(" now.") },
                Inlines(block));
        }

        [Fact]
        public void Parse_NonBreakingSpace_Kept()
        {
            var block = SingleBlock("<example>Max 10&#160;MB   per file.</example>");

            Assert.Equal(new[] { TextInline("Max 10\u00A0MB per file.") }, Inlines(block));
        }

        [Fact]
        public void Parse_SeeHrefHttp_HintIsUrl()
        {
            var block = SingleBlock("<example><see href=\"https://x.test/a\">docs</see></example>");

            Assert.Equal("https://x.test/a", block.Inlines.Single().Hint);
        }

        [Fact]
        public void Parse_SeeHrefHttp_IsLink()
        {
            var block = SingleBlock("<example>See <see href=\"https://x.test/a\">docs</see>.</example>");

            Assert.Equal(
                new[] { TextInline("See "), (DocInlineKind.Link, "docs", (string?)"https://x.test/a"), TextInline(".") },
                Inlines(block));
        }

        [Fact]
        public void Parse_SeeHrefWithoutText_UsesUrl()
        {
            var block = SingleBlock("<example><see href=\"http://y.test/\"/></example>");

            Assert.Equal(new[] { (DocInlineKind.Link, "http://y.test/", (string?)"http://y.test/") }, Inlines(block));
        }

        [Theory]
        [InlineData("file:///c:/x.exe")]
        [InlineData("javascript:alert(1)")]
        [InlineData("docs/page.html")]
        public void Parse_SeeHrefNonHttp_IsText(string href)
        {
            var block = SingleBlock($"<example><see href=\"{href}\">run</see></example>");

            Assert.Equal(new[] { TextInline("run") }, Inlines(block));
        }

        [Fact]
        public void Parse_SeeCref_IsSymbolLink()
        {
            var block = SingleBlock("<example>Call <see cref=\"M:Ns.F.Copy(System.String)\"/>.</example>");

            Assert.Equal(
                new[] { TextInline("Call "), (DocInlineKind.SymbolLink, "Copy", (string?)"M:Ns.F.Copy(System.String)"), TextInline(".") },
                Inlines(block));
        }

        [Fact]
        public void Parse_SeeCrefInnerText_IsSymbolLink()
        {
            var block = SingleBlock("<example><see cref=\"T:Ns.F\">the helper</see></example>");

            Assert.Equal(new[] { (DocInlineKind.SymbolLink, "the helper", (string?)"T:Ns.F") }, Inlines(block));
        }

        [Fact]
        public void Parse_SeealsoCref_IsSymbolLink()
        {
            var block = SingleBlock("<example><seealso cref=\"T:Ns.F\"/></example>");

            Assert.Equal(new[] { (DocInlineKind.SymbolLink, "F", (string?)"T:Ns.F") }, Inlines(block));
        }

        [Fact]
        public void Parse_UnresolvedCref_IsText()
        {
            var block = SingleBlock("<example><see cref=\"!:Bad\"/></example>");

            Assert.Equal(new[] { TextInline("Bad") }, Inlines(block));
        }

        [Fact]
        public void Parse_WhitespaceAroundLinks_Collapsed()
        {
            var block = SingleBlock("<example>  Use\n   <see href=\"https://x.test\">x</see>   now. </example>");

            Assert.Equal(
                new[] { TextInline("Use "), (DocInlineKind.Link, "x", (string?)"https://x.test"), TextInline(" now.") },
                Inlines(block));
        }

        [Fact]
        public void Parse_AdjacentPlainTextMerged()
        {
            var block = SingleBlock("<example>a <c>b</c> c</example>");

            Assert.Equal(new[] { TextInline("a b c") }, Inlines(block));
        }

        [Fact]
        public void Parse_Langword_KeywordClassification()
        {
            var block = SingleBlock("<example><see langword=\"null\"/></example>");

            Assert.Equal("keyword", block.Inlines.Single().Classification);
        }

        [Fact]
        public void Parse_Paramref_ParameterClassification()
        {
            var block = SingleBlock("<example><paramref name=\"src\"/></example>");

            Assert.Equal("parameter name", block.Inlines.Single().Classification);
        }

        [Fact]
        public void Parse_PlainText_TextClassification()
        {
            var block = SingleBlock("<example>plain</example>");

            Assert.Equal("text", block.Inlines.Single().Classification);
        }

        [Fact]
        public void Parse_DifferentClassifications_NotMerged()
        {
            var block = SingleBlock("<example>a <see langword=\"null\"/> b</example>");

            Assert.Equal(new[] { ("a ", "text"), ("null", "keyword"), (" b", "text") }, block.Inlines.Select(i => (i.Text, i.Classification)).ToArray());
        }

        [Fact]
        public void Parse_CodeBlock_SingleTextInline()
        {
            var block = SingleBlock("<example><code>x();</code></example>");

            Assert.Equal(DocBlockKind.Code, block.Kind);
            Assert.Equal(new[] { TextInline("x();") }, Inlines(block));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Parse_NullOrEmpty_ReturnsEmpty(string? xml)
        {
            Assert.Empty(DocParser.Parse(xml));
        }

        [Fact]
        public void Parse_NoExample_ReturnsEmpty()
        {
            Assert.Empty(DocParser.Parse(Doc("<summary>S</summary>")));
        }

        [Fact]
        public void Parse_EmptyExample_ReturnsEmpty()
        {
            Assert.Empty(DocParser.Parse(Doc("<example>   </example>")));
        }

        [Fact]
        public void Parse_CodeOnly()
        {
            var result = DocParser.Parse(Doc("<example><code>Copy(\"a.txt\", \"b.txt\");</code></example>"));

            Assert.Equal(new[] { Code("Copy(\"a.txt\", \"b.txt\");") }, Blocks(result));
        }

        [Fact]
        public void Parse_ProseAndCode_InOrder()
        {
            var result = DocParser.Parse(Doc("<example>Copy a file:\n <code>\n  Copy(\"a\", \"b\");\n </code>\n Then done.</example>"));

            Assert.Equal(new[] { Text("Copy a file:"), Code("Copy(\"a\", \"b\");"), Text("Then done.") }, Blocks(result));
        }

        [Fact]
        public void Parse_ProseWhitespaceCollapsed()
        {
            var result = DocParser.Parse(Doc("<example>  Use   this\n   method. </example>"));

            Assert.Equal(new[] { Text("Use this method.") }, Blocks(result));
        }

        [Fact]
        public void Parse_InlineElements()
        {
            var result = DocParser.Parse(Doc(
                "<example>Calls <see cref=\"M:Ns.F.Copy(System.String)\"/> with <paramref name=\"src\"/> for <typeparamref name=\"T\"/>, " +
                "returns <c>true</c> or <see langword=\"null\"/>.</example>"));

            Assert.Equal(new[] { Text("Calls Copy with src for T, returns true or null.") }, Blocks(result));
        }

        [Fact]
        public void Parse_SeeInnerTextAndHref()
        {
            var result = DocParser.Parse(Doc(
                "<example><see cref=\"T:Ns.F\">the helper</see>, <see href=\"https://x\">docs</see>, <see href=\"https://y\"/></example>"));

            Assert.Equal(new[] { Text("the helper, docs, https://y") }, Blocks(result));
        }

        [Fact]
        public void Parse_UnknownElement_UsesInnerText()
        {
            var result = DocParser.Parse(Doc("<example>Use <b>bold</b> text.</example>"));

            Assert.Equal(new[] { Text("Use bold text.") }, Blocks(result));
        }

        [Fact]
        public void Parse_Para_SplitsTextBlocks()
        {
            var result = DocParser.Parse(Doc("<example><para>One.</para><para>Two.</para></example>"));

            Assert.Equal(new[] { Text("One."), Text("Two.") }, Blocks(result));
        }

        [Fact]
        public void Parse_MultipleExamples_InOrder()
        {
            var result = DocParser.Parse(Doc("<example>A</example><summary>S</summary><example><code>b();</code></example>"));

            Assert.Equal(new[] { Text("A"), Code("b();") }, Blocks(result));
        }

        [Fact]
        public void Parse_Br_SplitsTextBlocks()
        {
            var result = DocParser.Parse(Doc("<example>First<br/>Second.</example>"));

            Assert.Equal(new[] { Text("First"), Text("Second.") }, Blocks(result));
        }

        [Fact]
        public void Parse_BulletList_OneBlockPerItem()
        {
            var result = DocParser.Parse(Doc(
                "<example>Intro <list type=\"bullet\"><item><description>One</description></item><item><description>Two</description></item></list> outro</example>"));

            Assert.Equal(new[] { Text("Intro"), Text("• One"), Text("• Two"), Text("outro") }, Blocks(result));
        }

        [Fact]
        public void Parse_NumberList_NumbersItems()
        {
            var result = DocParser.Parse(Doc(
                "<example><list type=\"number\"><item><description>One</description></item><item><description>Two</description></item></list></example>"));

            Assert.Equal(new[] { Text("1. One"), Text("2. Two") }, Blocks(result));
        }

        [Fact]
        public void Parse_ListTermAndDescription()
        {
            var result = DocParser.Parse(Doc(
                "<example><list type=\"table\"><item><term>A</term><description>alpha</description></item></list></example>"));

            Assert.Equal(new[] { Text("A – alpha") }, Blocks(result));
        }

        [Fact]
        public void Parse_CodeStartsOnTagLine()
        {
            var result = DocParser.Parse(Doc("\n    <example><code>var a = 1;\n    if (a > 0)\n        Run();\n    </code></example>\n"));

            Assert.Equal(new[] { Code("var a = 1;\nif (a > 0)\n    Run();") }, Blocks(result));
        }

        [Fact]
        public void Parse_EscapedEntitiesInCode()
        {
            var result = DocParser.Parse(Doc("<example><code>var x = new List&lt;int&gt;(); if (a &amp;&amp; b) {}</code></example>"));

            Assert.Equal(new[] { Code("var x = new List<int>(); if (a && b) {}") }, Blocks(result));
        }

        [Fact]
        public void Parse_MemberContentWithoutWrapper()
        {
            var result = DocParser.Parse("\n            <summary>S</summary>\n            <example>run</example>\n        ");

            Assert.Equal(new[] { Text("run") }, Blocks(result));
        }

        [Fact]
        public void Parse_SingleExampleWithoutWrapper()
        {
            var result = DocParser.Parse("\n            <example>run</example>\n        ");

            Assert.Equal(new[] { Text("run") }, Blocks(result));
        }

        [Fact]
        public void Parse_MalformedXml_ReturnsEmpty()
        {
            Assert.Empty(DocParser.Parse("<member><example><code>if (a < b)</code></example></member>"));
        }

        [Fact]
        public void Parse_BadlyFormedComment_ReturnsEmpty()
        {
            Assert.Empty(DocParser.Parse("<!-- Badly formed XML comment ignored for member \"M:X\" -->"));
        }
    }
}
