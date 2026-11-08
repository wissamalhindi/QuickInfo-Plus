using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuickInfoPlus.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace QuickInfoPlus.Core.Tests
{
    public class DocLookupTests
    {
        private const string Marker = "$$";

        private static readonly CSharpParseOptions ParseOptions = new CSharpParseOptions(documentationMode: DocumentationMode.Diagnose);

        private static readonly MetadataReference[] FrameworkReferences =
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
        };

        private const string FileHelperSource = @"
namespace Ns
{
    public class FileHelper
    {
        /// <summary>Copies a file.</summary>
        /// <example><code>Copy(""a.txt"", ""b.txt"");</code></example>
        public void {DECL}(string src, string dest) { }

        void Use()
        {
            {CALL}(""x"", ""y"");
        }
    }
}";

        private static async Task<(DocResult? Result, string Code, int Position)> FindAsync(
            string markedSource, MetadataReference[]? extraReferences = null, string[]? otherSources = null)
        {
            var position = markedSource.IndexOf(Marker, StringComparison.Ordinal);
            Assert.True(position >= 0, "source has no $$ marker");
            var code = markedSource.Remove(position, Marker.Length);

            var workspace = new AdhocWorkspace();
            var project = workspace.AddProject(ProjectInfo.Create(
                ProjectId.CreateNewId(),
                VersionStamp.Create(),
                "Test",
                "Test",
                LanguageNames.CSharp,
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                parseOptions: ParseOptions,
                metadataReferences: FrameworkReferences.Concat(extraReferences ?? Array.Empty<MetadataReference>())));

            var index = 0;
            foreach (var other in otherSources ?? Array.Empty<string>())
            {
                workspace.AddDocument(project.Id, $"Other{index++}.cs", SourceText.From(other));
            }

            var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(code));
            document = workspace.CurrentSolution.GetDocument(document.Id)!;

            var result = await DocLookup.FindAsync(document, position, CancellationToken.None);
            return (result, code, position);
        }

        private static (DocBlockKind, string)[] Blocks(DocResult? result) =>
            result!.Blocks.Select(b => (b.Kind, b.Text)).ToArray();

        [Fact]
        public async Task Find_MethodCallSite()
        {
            var (result, code, _) = await FindAsync(FileHelperSource.Replace("{DECL}", "Copy").Replace("{CALL}", "Co$$py"));

            Assert.Equal(new[] { (DocBlockKind.Code, "Copy(\"a.txt\", \"b.txt\");") }, Blocks(result));
            Assert.Equal("Copy", code.Substring(result!.SpanStart, result.SpanLength));
            Assert.Equal(code.IndexOf("Copy(\"x\"", StringComparison.Ordinal), result.SpanStart);
        }

        [Fact]
        public async Task Find_Declaration()
        {
            var (result, code, _) = await FindAsync(FileHelperSource.Replace("{DECL}", "Co$$py").Replace("{CALL}", "Copy"));

            Assert.Equal(new[] { (DocBlockKind.Code, "Copy(\"a.txt\", \"b.txt\");") }, Blocks(result));
            Assert.Equal(code.IndexOf("Copy(string", StringComparison.Ordinal), result!.SpanStart);
            Assert.Equal("Copy".Length, result.SpanLength);
        }

        [Fact]
        public async Task Find_PicksCorrectOverload()
        {
            var (result, _, _) = await FindAsync(@"
class C
{
    /// <example>two</example>
    void Copy(string a, string b) { }

    /// <example>three</example>
    void Copy(string a, string b, bool c) { }

    void Use() { Co$$py(""x"", ""y"", true); }
}");

            Assert.Equal(new[] { (DocBlockKind.Text, "three") }, Blocks(result));
        }

        [Fact]
        public async Task Find_GenericMethodCall()
        {
            var (result, _, _) = await FindAsync(@"
class C
{
    /// <example>map</example>
    static T Map<T>(T v) => v;

    void Use() { Ma$$p<int>(1); }
}");

            Assert.Equal(new[] { (DocBlockKind.Text, "map") }, Blocks(result));
        }

        [Fact]
        public async Task Find_ExtensionMethodCall()
        {
            var (result, _, _) = await FindAsync(@"
static class E
{
    /// <example>shout</example>
    public static string Shout(this string s) => s;
}

class C
{
    void Use() { ""x"".Sh$$out(); }
}");

            Assert.Equal(new[] { (DocBlockKind.Text, "shout") }, Blocks(result));
        }

        [Fact]
        public async Task Find_PartialClass()
        {
            var declaringPart = @"
partial class C
{
    /// <example>partial</example>
    void Run() { }
}";
            var (result, _, _) = await FindAsync(@"
partial class C
{
    void Use() { R$$un(); }
}", otherSources: new[] { declaringPart });

            Assert.Equal(new[] { (DocBlockKind.Text, "partial") }, Blocks(result));
        }

        private static MetadataReference EmitLibrary(string example, Func<string, string>? rewriteXml = null)
        {
            var directory = Path.Combine(Path.GetTempPath(), "QuickInfoPlusTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var dllPath = Path.Combine(directory, "Lib.dll");
            var xmlPath = Path.Combine(directory, "Lib.xml");

            var library = CSharpCompilation.Create(
                "Lib",
                new[] { CSharpSyntaxTree.ParseText(@"
namespace Lib
{
    public static class Util
    {
        /// <summary>Runs.</summary>
        /// <example>" + example + @"</example>
        public static void Run() { }
    }
}", ParseOptions) },
                FrameworkReferences,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (var pe = File.Create(dllPath))
            using (var xml = File.Create(xmlPath))
            {
                var emit = library.Emit(pe, xmlDocumentationStream: xml);
                Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
            }

            if (rewriteXml != null)
            {
                File.WriteAllText(xmlPath, rewriteXml(File.ReadAllText(xmlPath)));
            }

            return MetadataReference.CreateFromFile(dllPath, documentation: XmlDocumentationProvider.CreateFromFile(xmlPath));
        }

        private const string LibraryCall = @"
class C
{
    void Use() { Lib.Util.R$$un(); }
}";

        private static (DocInlineKind, string, string?)[] Inlines(DocResult? result) =>
            result!.Blocks.Single().Inlines.Select(i => (i.Kind, i.Text, i.Target)).ToArray();

        [Fact]
        public async Task Find_MetadataSymbolWithXmlDocs()
        {
            var (result, _, _) = await FindAsync(LibraryCall, extraReferences: new[] { EmitLibrary("run") });

            Assert.Equal(new[] { (DocBlockKind.Text, "run") }, Blocks(result));
        }

        [Fact]
        public async Task Find_CrefToSourceSymbol_IsSymbolLink()
        {
            var (result, _, _) = await FindAsync(@"
class C
{
    /// <example>Call <see cref=""Other""/>.</example>
    void Run() { }
    void Other() { }
    void Use() { R$$un(); }
}");

            Assert.Equal(
                new[]
                {
                    (DocInlineKind.Text, "Call ", null),
                    (DocInlineKind.SymbolLink, "C", (string?)"T:C"),
                    (DocInlineKind.Text, ".", null),
                    (DocInlineKind.SymbolLink, "Other", (string?)"M:C.Other"),
                    (DocInlineKind.Text, "()", null),
                    (DocInlineKind.Text, ".", null),
                },
                Inlines(result));
        }

        [Fact]
        public async Task Find_MetadataCref_IsSymbolLink()
        {
            var reference = EmitLibrary("<see cref=\"T:Lib.Util\"/>");

            var (result, _, _) = await FindAsync(LibraryCall, extraReferences: new[] { reference });

            Assert.Equal(
                new[] { (DocInlineKind.Text, "Lib", null), (DocInlineKind.Text, ".", null), (DocInlineKind.SymbolLink, "Util", (string?)"T:Lib.Util") },
                Inlines(result));
        }

        [Fact]
        public async Task Find_CrefNotInCompilation_BecomesText()
        {
            var reference = EmitLibrary("PLACEHOLDER", xml => xml.Replace("PLACEHOLDER", "<see cref=\"T:Gone.Missing\"/>"));

            var (result, _, _) = await FindAsync(LibraryCall, extraReferences: new[] { reference });

            Assert.Equal(new[] { (DocInlineKind.Text, "Gone.Missing", (string?)null) }, Inlines(result));
        }

        private const string SummaryNamesSource = @"
{USINGS}
public class FileHelper
{
    public FileHelper(string root) { }

    public string Root { get; set; }

    public bool Exists(string path) => true;

    public static T Map<T>(T value) => value;

    /// <example>{EXAMPLE}</example>
    public void Run() { }

    void Use() { R$$un(); }
}";

        private static async Task<(DocInlineKind, string, string?)[]> SummaryNameAsync(string example, string usings = "")
        {
            var (result, _, _) = await FindAsync(SummaryNamesSource.Replace("{EXAMPLE}", example).Replace("{USINGS}", usings));
            return Inlines(result);
        }

        [Theory]
        [InlineData("<see cref=\"Exists(string)\"/>", "FileHelper.Exists(string)")]
        [InlineData("<see cref=\"Root\"/>", "FileHelper.Root")]
        [InlineData("<see cref=\"FileHelper\"/>", "FileHelper")]
        [InlineData("<see cref=\"FileHelper(string)\"/>", "FileHelper(string)")]
        [InlineData("<see cref=\"Map{T}(T)\"/>", "FileHelper.Map<T>(T)")]
        [InlineData("<seealso cref=\"Exists(string)\"/>", "FileHelper.Exists(string)")]
        public async Task Find_Cref_LikeSummary(string example, string expected)
        {
            var inlines = await SummaryNameAsync(example);

            Assert.Equal(expected, string.Concat(inlines.Select(i => i.Item2)));
        }

        [Fact]
        public async Task Find_CrefProperty_SplitIntoLinks()
        {
            var inlines = await SummaryNameAsync("<see cref=\"Root\"/>");

            Assert.Equal(
                new[]
                {
                    (DocInlineKind.SymbolLink, "FileHelper", (string?)"T:FileHelper"),
                    (DocInlineKind.Text, ".", null),
                    (DocInlineKind.SymbolLink, "Root", "P:FileHelper.Root"),
                },
                inlines);
        }

        [Fact]
        public async Task Find_CrefMethod_ParameterTypesAreLinks()
        {
            var inlines = await SummaryNameAsync("<see cref=\"Exists(string)\"/>");

            Assert.Equal(
                new[]
                {
                    (DocInlineKind.SymbolLink, "FileHelper", (string?)"T:FileHelper"),
                    (DocInlineKind.Text, ".", null),
                    (DocInlineKind.SymbolLink, "Exists", "M:FileHelper.Exists(System.String)"),
                    (DocInlineKind.Text, "(", null),
                    (DocInlineKind.SymbolLink, "string", "T:System.String"),
                    (DocInlineKind.Text, ")", null),
                },
                inlines);
        }

        [Fact]
        public async Task Find_CrefGeneric_TypeParametersPlain()
        {
            var inlines = await SummaryNameAsync("<see cref=\"Map{T}(T)\"/>");

            Assert.Equal(
                new[]
                {
                    (DocInlineKind.SymbolLink, "FileHelper", (string?)"T:FileHelper"),
                    (DocInlineKind.Text, ".", null),
                    (DocInlineKind.SymbolLink, "Map", "M:FileHelper.Map``1(``0)"),
                    (DocInlineKind.Text, "<", null),
                    (DocInlineKind.Text, "T", null),
                    (DocInlineKind.Text, ">(", null),
                    (DocInlineKind.Text, "T", null),
                    (DocInlineKind.Text, ")", null),
                },
                inlines);
        }

        [Fact]
        public async Task Find_CrefNamespace_Plain()
        {
            var inlines = await SummaryNameAsync("<see cref=\"System.IO.File\"/>");

            Assert.Equal(
                new[]
                {
                    (DocInlineKind.Text, "System", null),
                    (DocInlineKind.Text, ".", null),
                    (DocInlineKind.Text, "IO", null),
                    (DocInlineKind.Text, ".", null),
                    (DocInlineKind.SymbolLink, "File", (string?)"T:System.IO.File"),
                },
                inlines);
        }

        [Fact]
        public async Task Find_CrefParts_HaveHoverHints()
        {
            var (result, _, _) = await FindAsync(SummaryNamesSource.Replace("{EXAMPLE}", "<see cref=\"Exists(string)\"/>").Replace("{USINGS}", ""));

            var links = result!.Blocks.Single().Inlines.Where(i => i.Kind == DocInlineKind.SymbolLink).ToDictionary(i => i.Text, i => i.Hint);
            Assert.Equal("FileHelper", links["FileHelper"]);
            Assert.Equal("string", links["string"]);
            Assert.Contains("FileHelper.Exists(string path)", links["Exists"]);
        }

        [Fact]
        public async Task Find_CrefCustomText_HintIsSummaryName()
        {
            var inline = (await FindAsync(SummaryNamesSource.Replace("{EXAMPLE}", "<see cref=\"Exists(string)\">check</see>").Replace("{USINGS}", ""))).Result!
                .Blocks.Single().Inlines.Single();

            Assert.Equal("FileHelper.Exists(string)", inline.Hint);
        }

        [Fact]
        public async Task Find_ExceptionType_HasHoverHint()
        {
            var (result, _, _) = await FindAsync(@"
using System;

class C
{
    /// <exception cref=""ArgumentNullException"">bad</exception>
    void Run() { }

    void Use() { R$$un(); }
}");

            Assert.Equal("ArgumentNullException", result!.Exceptions.Single().Type.Single().Hint);
        }

        [Fact]
        public async Task Find_CrefCustomText_Kept()
        {
            var inlines = await SummaryNameAsync("<see cref=\"Exists(string)\">check</see>");

            Assert.Equal(new[] { (DocInlineKind.SymbolLink, "check", (string?)"M:FileHelper.Exists(System.String)") }, inlines);
        }

        [Theory]
        [InlineData("using System.IO;", "File")]
        [InlineData("", "System.IO.File")]
        [InlineData("using IOFile = System.IO.File;", "System.IO.File")]
        [InlineData("using System.IO; using IOFile = System.IO.File;", "File")]
        [InlineData("using IO = System.IO;", "System.IO.File")]
        public async Task Find_CrefMinimalQualification(string usings, string expected)
        {
            var inlines = await SummaryNameAsync("<see cref=\"System.IO.File\"/>", usings);

            Assert.Equal(expected, string.Concat(inlines.Select(i => i.Item2)));
        }

        [Fact]
        public async Task Find_CrefThroughAlias_ShowsTypeName()
        {
            var inlines = await SummaryNameAsync("<see cref=\"Exists(string)\"/>", "using Helper = FileHelper;");

            Assert.Equal(
                new[]
                {
                    (DocInlineKind.SymbolLink, "FileHelper", (string?)"T:FileHelper"),
                    (DocInlineKind.Text, ".", null),
                    (DocInlineKind.SymbolLink, "Exists", "M:FileHelper.Exists(System.String)"),
                    (DocInlineKind.Text, "(", null),
                    (DocInlineKind.SymbolLink, "string", "T:System.String"),
                    (DocInlineKind.Text, ")", null),
                },
                inlines);
        }

        [Fact]
        public async Task Find_CrefCustomTextThroughAlias_HintIsTypeName()
        {
            var inline = (await FindAsync(SummaryNamesSource.Replace("{EXAMPLE}", "<see cref=\"Exists(string)\">check</see>").Replace("{USINGS}", "using Helper = FileHelper;"))).Result!
                .Blocks.Single().Inlines.Single();

            Assert.Equal("FileHelper.Exists(string)", inline.Hint);
        }

        [Fact]
        public async Task Find_TypeParamRefThroughAlias_ShowsTypeName()
        {
            var (result, _, _) = await FindAsync(@"
using Helper = D;

class D { }

class C
{
    /// <example><typeparamref name=""T""/></example>
    static T Map<T>(T v) => v;

    void Use() { Ma$$p<Helper>(null); }
}");

            Assert.Equal(new[] { (DocInlineKind.SymbolLink, "D", (string?)"T:D") }, Inlines(result));
        }

        [Fact]
        public async Task Find_CrefMethod_Classifications()
        {
            var (result, _, _) = await FindAsync(SummaryNamesSource.Replace("{EXAMPLE}", "<see cref=\"Exists(string)\"/>").Replace("{USINGS}", ""));

            Assert.Equal(
                new[] { "class name", "punctuation", "method name", "punctuation", "keyword", "punctuation" },
                result!.Blocks.Single().Inlines.Select(i => i.Classification).ToArray());
        }

        [Fact]
        public async Task Find_CrefProperty_Classification()
        {
            var (result, _, _) = await FindAsync(SummaryNamesSource.Replace("{EXAMPLE}", "<see cref=\"Root\"/>").Replace("{USINGS}", ""));

            Assert.Equal("property name", result!.Blocks.Single().Inlines.Last().Classification);
        }

        [Fact]
        public async Task Find_CrefExtensionMethod_Classification()
        {
            var (result, _, _) = await FindAsync(@"
static class E
{
    public static string Shout(this string s) => s;

    /// <example><see cref=""Shout(string)""/></example>
    public static void Run() { }

    static void Use() { R$$un(); }
}");

            var shout = result!.Blocks.Single().Inlines.Single(i => i.Text == "Shout");
            Assert.Equal("extension method name", shout.Classification);
        }

        [Fact]
        public async Task Find_TypeParamRef_Classifications()
        {
            const string source = @"
class C
{
    /// <example><typeparamref name=""T""/></example>
    static T {DECL}<T>(T v) => v;

    void Use() { {CALL}<int>(1); }
}";

            var (atCall, _, _) = await FindAsync(source.Replace("{DECL}", "Map").Replace("{CALL}", "Ma$$p"));
            var (atDeclaration, _, _) = await FindAsync(source.Replace("{DECL}", "Ma$$p").Replace("{CALL}", "Map"));

            Assert.Equal("keyword", atCall!.Blocks.Single().Inlines.Single().Classification);
            Assert.Equal("type parameter name", atDeclaration!.Blocks.Single().Inlines.Single().Classification);
        }

        [Theory]
        [InlineData("record Person { }", "record class name")]
        [InlineData("record struct Point { }", "record struct name")]
        public async Task Find_CrefRecord_Classification(string declaration, string expected)
        {
            var name = declaration.Contains("Person") ? "Person" : "Point";
            var (result, _, _) = await FindAsync(@"
namespace System.Runtime.CompilerServices { class IsExternalInit { } }

" + declaration + @"

class C
{
    /// <example><see cref=""" + name + @"""/></example>
    void Run() { }

    void Use() { R$$un(); }
}");

            Assert.Equal(expected, result!.Blocks.Single().Inlines.Single().Classification);
        }

        [Fact]
        public async Task Find_TypeParamRef_ShadowedByMethod_UsesInnermost()
        {
            var (result, _, _) = await FindAsync(@"
class Box<T>
{
    /// <example><typeparamref name=""T""/></example>
#pragma warning disable CS0693
    public T Get<T>() => default;
#pragma warning restore CS0693
}

class C
{
    void Use() { new Box<string>().Ge$$t<int>(); }
}");

            Assert.Equal("int", result!.Blocks.Single().Inlines.Single().Text);
        }

        [Fact]
        public async Task Find_AliasHover_ShowsTargetDocs()
        {
            var (result, _, _) = await FindAsync(@"
using Helper = C;

/// <example>run</example>
class C { }

class D
{
    void Use() { Help$$er h = null; }
}");

            Assert.Equal(new[] { (DocBlockKind.Text, "run") }, Blocks(result));
        }

        [Fact]
        public async Task Find_TypeParamRef_GenericCall()
        {
            var (result, _, _) = await FindAsync(@"
class C
{
    /// <example>Returns <typeparamref name=""T""/>.</example>
    static T Map<T>(T v) => v;

    void Use() { Ma$$p<int>(1); }
}");

            Assert.Equal(
                new[] { (DocInlineKind.Text, "Returns ", null), (DocInlineKind.SymbolLink, "int", (string?)"T:System.Int32"), (DocInlineKind.Text, ".", null) },
                Inlines(result));
        }

        [Fact]
        public async Task Find_TypeParamRef_Declaration()
        {
            var (result, _, _) = await FindAsync(@"
class C
{
    /// <example>Returns <typeparamref name=""T""/>.</example>
    static T Ma$$p<T>(T v) => v;
}");

            Assert.Equal(
                new[] { (DocInlineKind.Text, "Returns ", null), (DocInlineKind.Text, "T", null), (DocInlineKind.Text, ".", (string?)null) },
                Inlines(result));
        }

        [Fact]
        public async Task Find_TypeParamRef_ContainingType()
        {
            var (result, _, _) = await FindAsync(@"
class Box<T>
{
    /// <example><typeparamref name=""T""/></example>
    public void Put() { }
}

class C
{
    void Use() { new Box<string>().P$$ut(); }
}");

            Assert.Equal(new[] { (DocInlineKind.SymbolLink, "string", (string?)"T:System.String") }, Inlines(result));
        }

        [Fact]
        public async Task Find_TypeParamRef_InferredExtension()
        {
            var (result, _, _) = await FindAsync(@"
static class E
{
    /// <example><typeparamref name=""T""/></example>
    public static T Echo<T>(this T v) => v;
}

class C
{
    void Use() { 1.Ec$$ho(); }
}");

            Assert.Equal(new[] { (DocInlineKind.SymbolLink, "int", (string?)"T:System.Int32") }, Inlines(result));
        }

        [Fact]
        public async Task Find_TypeParamRef_Unknown()
        {
            var (result, _, _) = await FindAsync(@"
class C
{
    /// <example><typeparamref name=""X""/></example>
    void Run() { }

    void Use() { R$$un(); }
}");

            Assert.Equal(new[] { (DocInlineKind.Text, "X", (string?)null) }, Inlines(result));
        }

        [Fact]
        public async Task Find_ExceptionsOnly_ReturnsResult()
        {
            var (result, _, _) = await FindAsync(@"
using System;

class C
{
    /// <exception cref=""ArgumentNullException""><paramref name=""path""/> is null.</exception>
    void Run(string path) { }

    void Use() { R$$un(null); }
}");

            Assert.NotNull(result);
            Assert.Empty(result!.Blocks);
            var entry = Assert.Single(result.Exceptions);
            Assert.Equal(
                new[] { (DocInlineKind.SymbolLink, "ArgumentNullException", (string?)"T:System.ArgumentNullException") },
                entry.Type.Select(i => (i.Kind, i.Text, i.Target)).ToArray());
            Assert.Equal("path is null.", Assert.Single(entry.Description).Text);
        }

        [Fact]
        public async Task Find_ExceptionWithoutDescriptions_NoExample_ReturnsNull()
        {
            var (result, _, _) = await FindAsync(@"
using System;

class C
{
    /// <exception cref=""ArgumentNullException""/>
    void Run() { }

    void Use() { R$$un(); }
}");

            Assert.Null(result);
        }

        [Fact]
        public async Task Find_ExceptionDescriptionCref_LikeSummary()
        {
            var (result, _, _) = await FindAsync(@"
using System;

class C
{
    /// <exception cref=""InvalidOperationException"">Call <see cref=""Run""/> first.</exception>
    void Run() { }

    void Use() { R$$un(); }
}");

            var description = Assert.Single(Assert.Single(result!.Exceptions).Description);
            Assert.Equal(
                new[]
                {
                    (DocInlineKind.Text, "Call ", null),
                    (DocInlineKind.SymbolLink, "C", (string?)"T:C"),
                    (DocInlineKind.Text, ".", null),
                    (DocInlineKind.SymbolLink, "Run", "M:C.Run"),
                    (DocInlineKind.Text, "()", null),
                    (DocInlineKind.Text, " first.", null),
                },
                description.Inlines.Select(i => (i.Kind, i.Text, i.Target)).ToArray());
        }

        private const string BothSectionsSource = @"
using System;

class C
{
    /// <example>run</example>
    /// <exception cref=""InvalidOperationException"">not ready</exception>
    void Run() { }

    void Use() { R$$un(); }
}";

        private static async Task<DocResult?> FindWithSectionsAsync(string markedSource, DocSections sections)
        {
            var position = markedSource.IndexOf(Marker, StringComparison.Ordinal);
            var code = markedSource.Remove(position, Marker.Length);
            var workspace = new AdhocWorkspace();
            var project = workspace.AddProject(ProjectInfo.Create(
                ProjectId.CreateNewId(),
                VersionStamp.Create(),
                "Test",
                "Test",
                LanguageNames.CSharp,
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                parseOptions: ParseOptions,
                metadataReferences: FrameworkReferences));
            var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(code));
            return await DocLookup.FindAsync(workspace.CurrentSolution.GetDocument(document.Id)!, position, sections, CancellationToken.None);
        }

        [Fact]
        public async Task Find_ExamplesOff_ExceptionsOnly()
        {
            var result = await FindWithSectionsAsync(BothSectionsSource, new DocSections(examples: false, exceptions: true));

            Assert.Empty(result!.Blocks);
            Assert.Single(result.Exceptions);
        }

        [Fact]
        public async Task Find_ExceptionsOff_ExamplesOnly()
        {
            var result = await FindWithSectionsAsync(BothSectionsSource, new DocSections(examples: true, exceptions: false));

            Assert.Single(result!.Blocks);
            Assert.Empty(result.Exceptions);
        }

        [Fact]
        public async Task Find_BothOff_ReturnsNull()
        {
            Assert.Null(await FindWithSectionsAsync(BothSectionsSource, new DocSections(examples: false, exceptions: false)));
        }

        [Fact]
        public async Task Find_ExamplesOff_NoExceptions_ReturnsNull()
        {
            var source = BothSectionsSource.Replace("/// <exception cref=\"InvalidOperationException\">not ready</exception>", "");
            Assert.DoesNotContain("<exception", source);

            Assert.Null(await FindWithSectionsAsync(source, new DocSections(examples: false, exceptions: true)));
        }

        [Fact]
        public async Task Find_CodeStartsOnTagLine_SourceSymbol()
        {
            var (result, _, _) = await FindAsync(@"
class C
{
    /// <example><code>var a = 1;
    /// if (a > 0)
    ///     Run();
    /// </code></example>
    void Run() { }

    void Use() { R$$un(); }
}");

            Assert.Equal(new[] { (DocBlockKind.Code, "var a = 1;\nif (a > 0)\n    Run();") }, Blocks(result));
        }

        [Fact]
        public async Task Find_CodeStartsOnTagLine_MetadataSymbol()
        {
            var reference = EmitLibrary("<code>var a = 1;\n        /// if (a > 0)\n        ///     Run();\n        /// </code>");

            var (result, _, _) = await FindAsync(LibraryCall, extraReferences: new[] { reference });

            Assert.Equal(new[] { (DocBlockKind.Code, "var a = 1;\nif (a > 0)\n    Run();") }, Blocks(result));
        }

        [Fact]
        public async Task Find_NoDocs_ReturnsNull()
        {
            var (result, _, _) = await FindAsync(@"
class C
{
    void Run() { }
    void Use() { R$$un(); }
}");

            Assert.Null(result);
        }

        [Fact]
        public async Task Find_SummaryOnly_ReturnsNull()
        {
            var (result, _, _) = await FindAsync(@"
class C
{
    /// <summary>Runs.</summary>
    void Run() { }
    void Use() { R$$un(); }
}");

            Assert.Null(result);
        }

        [Theory]
        [InlineData("class C\n{\n    /// <example>run</example>\n    pub$$lic static void Run() { }\n}")]
        [InlineData("class C\n{\n    /// <example>run</example>\n    public static void Run() { }\n$$\n    public static void Other() { }\n}")]
        public async Task Find_OnKeywordOrWhitespace_ReturnsNull(string source)
        {
            var (result, _, _) = await FindAsync(source);

            Assert.Null(result);
        }
    }
}
