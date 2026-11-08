using QuickInfoPlus.Core;
using Xunit;

namespace QuickInfoPlus.Core.Tests
{
    public class CodeIndentationTests
    {
        [Fact]
        public void Normalize_CodeOnTagLine_RemovesTagLineIndentation()
        {
            Assert.Equal(
                "var a = 1;\nif (a > 0)\n    Run();",
                CodeIndentation.Normalize("var a = 1;\n    if (a > 0)\n        Run();", tagLineIndentation: "    "));
        }

        [Fact]
        public void Normalize_CodeOnTagLine_KeepsRelativeIndent()
        {
            Assert.Equal("if (x)\n    y();", CodeIndentation.Normalize("if (x)\n        y();", tagLineIndentation: "    "));
        }

        [Fact]
        public void Normalize_CodeOnTagLine_MetadataIndentation()
        {
            Assert.Equal(
                "var a = 1;\nif (a > 0)\n    Run();",
                CodeIndentation.Normalize("var a = 1;\n            if (a > 0)\n                Run();\n        ", tagLineIndentation: "            "));
        }

        [Fact]
        public void Normalize_CodeOnTagLine_LessIndentedLine_RemovesOnlySharedPart()
        {
            Assert.Equal("a();\nb();", CodeIndentation.Normalize("a();\n  b();", tagLineIndentation: "    "));
        }

        [Fact]
        public void Normalize_CodeOnNextLine_IgnoresTagLineIndentation()
        {
            Assert.Equal("a();\n  b();", CodeIndentation.Normalize("\n    a();\n      b();", tagLineIndentation: "  "));
        }

        [Fact]
        public void Normalize_RemovesSharedIndentationAndOuterBlankLines()
        {
            var input = "\n            var a = 1;\n            if (a > 0)\n            {\n                Run();\n            }\n        ";

            Assert.Equal("var a = 1;\nif (a > 0)\n{\n    Run();\n}", CodeIndentation.Normalize(input));
        }

        [Fact]
        public void Normalize_KeepsInnerBlankLines()
        {
            Assert.Equal("a();\n\nb();", CodeIndentation.Normalize("  a();\n\n  b();"));
        }

        [Fact]
        public void Normalize_WhitespaceOnlyLinesIgnoredWhenMeasuring()
        {
            Assert.Equal("a();\n\nb();", CodeIndentation.Normalize("    a();\n  \n    b();"));
        }

        [Fact]
        public void Normalize_LessIndentedLineSetsFloor()
        {
            Assert.Equal("  a();\nb();\n  c();", CodeIndentation.Normalize("    a();\n  b();\n    c();"));
        }

        [Fact]
        public void Normalize_TabsShared()
        {
            Assert.Equal("a();\n\tb();", CodeIndentation.Normalize("\ta();\n\t\tb();"));
        }

        [Fact]
        public void Normalize_MixedTabsAndSpaces_RemovesOnlySharedPrefix()
        {
            Assert.Equal("\ta();\n    b();", CodeIndentation.Normalize("\ta();\n    b();"));
        }

        [Fact]
        public void Normalize_CrLf()
        {
            Assert.Equal("a();\nb();", CodeIndentation.Normalize("  a();\r\n  b();"));
        }

        [Fact]
        public void Normalize_WhitespaceOnly_ReturnsEmpty()
        {
            Assert.Equal("", CodeIndentation.Normalize("  \n \t \n"));
        }
    }
}
