using QuickInfoPlus.Core;
using Xunit;

namespace QuickInfoPlus.Core.Tests
{
    public class CrefFormatterTests
    {
        [Theory]
        [InlineData("M:Ns.FileHelper.Copy(System.String,System.String)", "Copy")]
        [InlineData("T:Ns.FileHelper", "FileHelper")]
        [InlineData("T:System.Collections.Generic.List`1", "List")]
        [InlineData("M:Ns.FileHelper.#ctor", "FileHelper")]
        [InlineData("M:Ns.FileHelper.#ctor(System.String)", "FileHelper")]
        [InlineData("P:Ns.FileHelper.Name", "Name")]
        [InlineData("M:Ns.Util.Map``1(``0)", "Map")]
        [InlineData("!:Bad", "Bad")]
        [InlineData("Copy", "Copy")]
        public void ToDisplayName_ReturnsShortName(string cref, string expected)
        {
            Assert.Equal(expected, CrefFormatter.ToDisplayName(cref));
        }
    }
}
