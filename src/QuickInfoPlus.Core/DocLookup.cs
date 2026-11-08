using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.FindSymbols;

namespace QuickInfoPlus.Core
{
    public static class DocLookup
    {
        // Same format Roslyn uses for crefs in the Quick Info summary (ISymbolExtensions2.CrefFormat).
        private static readonly SymbolDisplayFormat CrefFormat = CreateCrefFormat(SymbolDisplayTypeQualificationStyle.NameOnly);

        private static readonly SymbolDisplayFormat ConstructorCrefFormat =
            CrefFormat.WithMemberOptions(SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeExplicitInterface);

        private static readonly SymbolDisplayFormat AliasTargetFormat =
            CreateCrefFormat(SymbolDisplayTypeQualificationStyle.NameAndContainingTypes);

        private static readonly SymbolDisplayFormat QualifiedAliasTargetFormat =
            CreateCrefFormat(SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

        private static SymbolDisplayFormat CreateCrefFormat(SymbolDisplayTypeQualificationStyle typeQualificationStyle) =>
            new SymbolDisplayFormat(
                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
                typeQualificationStyle: typeQualificationStyle,
                propertyStyle: SymbolDisplayPropertyStyle.NameOnly,
                genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeVariance,
                memberOptions: SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeExplicitInterface | SymbolDisplayMemberOptions.IncludeContainingType,
                parameterOptions: SymbolDisplayParameterOptions.IncludeParamsRefOut | SymbolDisplayParameterOptions.IncludeType,
                miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

        public static Task<DocResult?> FindAsync(Document document, int position, CancellationToken cancellationToken) =>
            FindAsync(document, position, DocSections.All, cancellationToken);

        public static async Task<DocResult?> FindAsync(Document document, int position, DocSections sections, CancellationToken cancellationToken)
        {
            if (!sections.Examples && !sections.Exceptions)
            {
                return null;
            }

            var hovered = await SymbolFinder.FindSymbolAtPositionAsync(document, position, cancellationToken).ConfigureAwait(false);
            if (hovered is IAliasSymbol alias)
            {
                hovered = alias.Target;
            }

            if (hovered == null)
            {
                return null;
            }

            var typeResolution = hovered;
            var symbol = hovered;
            if (hovered is IMethodSymbol { ReducedFrom: { } reducedFrom } reduced)
            {
                typeResolution = reduced.GetConstructedReducedFrom() ?? reducedFrom;
                symbol = reducedFrom;
            }

            symbol = symbol.OriginalDefinition;

            var xml = symbol.GetDocumentationCommentXml(expandIncludes: false, cancellationToken: cancellationToken);
            var blocks = sections.Examples ? DocParser.Parse(xml) : Array.Empty<DocBlock>();
            var exceptions = sections.Exceptions ? DocParser.ParseExceptions(xml) : Array.Empty<ExceptionEntry>();
            if (blocks.Count == 0 && exceptions.Count == 0)
            {
                return null;
            }

            if (blocks.Any(b => b.Inlines.Any(NeedsSemanticModel)) || exceptions.Count > 0)
            {
                var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
                blocks = blocks.Select(b => ResolveBlock(b, symbol, typeResolution, semanticModel, position)).ToList();
                exceptions = exceptions.Select(e => new ExceptionEntry(
                    DocParser.MergeAdjacentText(ResolveInlines(e.Type, symbol, typeResolution, semanticModel, position)),
                    e.Description.Select(b => ResolveBlock(b, symbol, typeResolution, semanticModel, position)).ToList())).ToList();
            }

            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root == null)
            {
                return null;
            }

            var span = root.FindToken(position).Span;
            return new DocResult(blocks, exceptions, span.Start, span.Length);
        }

        private static bool NeedsSemanticModel(DocInline inline) =>
            inline.Kind == DocInlineKind.SymbolLink || inline.Kind == DocInlineKind.TypeParameterReference;

        private static DocBlock ResolveBlock(DocBlock block, ISymbol definition, ISymbol typeResolution, SemanticModel? semanticModel, int position)
        {
            if (!block.Inlines.Any(NeedsSemanticModel))
            {
                return block;
            }

            var inlines = ResolveInlines(block.Inlines, definition, typeResolution, semanticModel, position);
            return new DocBlock(block.Kind, DocParser.MergeAdjacentText(inlines));
        }

        private static IEnumerable<DocInline> ResolveInlines(
            IEnumerable<DocInline> inlines, ISymbol definition, ISymbol typeResolution, SemanticModel? semanticModel, int position)
        {
            return inlines.SelectMany(inline =>
            {
                switch (inline.Kind)
                {
                    case DocInlineKind.SymbolLink:
                        return ResolveSymbolLink(inline, semanticModel, position);
                    case DocInlineKind.TypeParameterReference:
                        return ResolveTypeParameter(inline.Target ?? inline.Text, definition, typeResolution, semanticModel, position);
                    default:
                        return new[] { inline };
                }
            });
        }

        private static IEnumerable<DocInline> ResolveSymbolLink(DocInline inline, SemanticModel? semanticModel, int position)
        {
            var target = inline.Target!;
            var referenced = semanticModel == null ? null : DocumentationCommentId.GetFirstSymbolForDeclarationId(target, semanticModel.Compilation);
            if (referenced == null || semanticModel == null)
            {
                return new[] { new DocInline(DocInlineKind.Text, inline.HasCustomText ? inline.Text : TrimCrefPrefix(target)) };
            }

            if (inline.HasCustomText)
            {
                var hint = string.Concat(MinimalParts(referenced, semanticModel, position, CrefFormat));
                return new[] { new DocInline(inline.Kind, inline.Text, inline.Target, inline.HasCustomText, inline.Classification, hint) };
            }

            var format = referenced is IMethodSymbol { MethodKind: MethodKind.Constructor } ? ConstructorCrefFormat : CrefFormat;
            return ToInlines(MinimalParts(referenced, semanticModel, position, format));
        }

        private static IEnumerable<DocInline> ResolveTypeParameter(string name, ISymbol definition, ISymbol typeResolution, SemanticModel? semanticModel, int position)
        {
            if (semanticModel != null)
            {
                // Last match: a method's own type parameter hides a same-named one of its containing type.
                var index = AllTypeParameters(definition).FindLastIndex(p => p.Name == name);
                var arguments = AllTypeArguments(typeResolution);
                if (index >= 0 && index < arguments.Count)
                {
                    return ToInlines(MinimalParts(arguments[index], semanticModel, position, CrefFormat));
                }
            }

            return new[] { new DocInline(DocInlineKind.Text, name, classification: ClassificationTypeNames.TypeParameterName) };
        }

        // Minimal names prefer a using alias in scope (Helper.Exists); the docs name the real type, so the alias is
        // replaced by its target, qualified as it would be without the alias.
        private static IEnumerable<SymbolDisplayPart> MinimalParts(ISymbol symbol, SemanticModel semanticModel, int position, SymbolDisplayFormat format) =>
            symbol.ToMinimalDisplayParts(semanticModel, position, format).SelectMany(part =>
                part.Symbol is IAliasSymbol alias ? AliasTargetParts(alias.Target, semanticModel, position) : new[] { part });

        private static IEnumerable<SymbolDisplayPart> AliasTargetParts(INamespaceOrTypeSymbol target, SemanticModel semanticModel, int position)
        {
            if (target is ITypeSymbol type)
            {
                var outermost = type;
                while (outermost.ContainingType != null)
                {
                    outermost = outermost.ContainingType;
                }

                var inScope = semanticModel.LookupNamespacesAndTypes(position, name: outermost.Name)
                    .Any(s => SymbolEqualityComparer.Default.Equals(s, outermost.OriginalDefinition));
                if (inScope)
                {
                    return target.ToDisplayParts(AliasTargetFormat);
                }
            }

            return target.ToDisplayParts(QualifiedAliasTargetFormat);
        }

        private static IEnumerable<DocInline> ToInlines(IEnumerable<SymbolDisplayPart> parts)
        {
            foreach (var part in parts)
            {
                var symbol = part.Symbol;
                var id = symbol == null || symbol is INamespaceSymbol || symbol is ITypeParameterSymbol
                    ? null
                    : symbol.OriginalDefinition.GetDocumentationCommentId();
                var classification = Classify(part);
                yield return id == null
                    ? new DocInline(DocInlineKind.Text, part.ToString(), classification: classification)
                    : new DocInline(DocInlineKind.SymbolLink, part.ToString(), id, classification: classification,
                        hint: symbol!.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
            }
        }

        private static string Classify(SymbolDisplayPart part)
        {
            switch (part.Kind)
            {
                case SymbolDisplayPartKind.ClassName: return ClassificationTypeNames.ClassName;
                case SymbolDisplayPartKind.RecordClassName: return ClassificationTypeNames.RecordClassName;
                case SymbolDisplayPartKind.StructName: return ClassificationTypeNames.StructName;
                case SymbolDisplayPartKind.RecordStructName: return ClassificationTypeNames.RecordStructName;
                case SymbolDisplayPartKind.InterfaceName: return ClassificationTypeNames.InterfaceName;
                case SymbolDisplayPartKind.EnumName: return ClassificationTypeNames.EnumName;
                case SymbolDisplayPartKind.DelegateName: return ClassificationTypeNames.DelegateName;
                case SymbolDisplayPartKind.TypeParameterName: return ClassificationTypeNames.TypeParameterName;
                case SymbolDisplayPartKind.MethodName:
                    return part.Symbol is IMethodSymbol { IsExtensionMethod: true }
                        ? ClassificationTypeNames.ExtensionMethodName
                        : ClassificationTypeNames.MethodName;
                case SymbolDisplayPartKind.ExtensionMethodName: return ClassificationTypeNames.ExtensionMethodName;
                case SymbolDisplayPartKind.PropertyName: return ClassificationTypeNames.PropertyName;
                case SymbolDisplayPartKind.FieldName: return ClassificationTypeNames.FieldName;
                case SymbolDisplayPartKind.EventName: return ClassificationTypeNames.EventName;
                case SymbolDisplayPartKind.EnumMemberName: return ClassificationTypeNames.EnumMemberName;
                case SymbolDisplayPartKind.ConstantName: return ClassificationTypeNames.ConstantName;
                case SymbolDisplayPartKind.NamespaceName: return ClassificationTypeNames.NamespaceName;
                case SymbolDisplayPartKind.ParameterName: return ClassificationTypeNames.ParameterName;
                case SymbolDisplayPartKind.LocalName: return ClassificationTypeNames.LocalName;
                case SymbolDisplayPartKind.Keyword: return ClassificationTypeNames.Keyword;
                case SymbolDisplayPartKind.Punctuation: return ClassificationTypeNames.Punctuation;
                case SymbolDisplayPartKind.Operator: return ClassificationTypeNames.Operator;
                case SymbolDisplayPartKind.NumericLiteral: return ClassificationTypeNames.NumericLiteral;
                case SymbolDisplayPartKind.StringLiteral: return ClassificationTypeNames.StringLiteral;
                default: return ClassificationTypeNames.Text;
            }
        }

        private static List<ITypeParameterSymbol> AllTypeParameters(ISymbol symbol)
        {
            var result = ContainingTypesOutermostFirst(symbol).SelectMany(t => t.TypeParameters).ToList();
            if (symbol is IMethodSymbol method)
            {
                result.AddRange(method.TypeParameters);
            }

            return result;
        }

        private static List<ITypeSymbol> AllTypeArguments(ISymbol symbol)
        {
            var result = ContainingTypesOutermostFirst(symbol).SelectMany(t => t.TypeArguments).ToList();
            if (symbol is IMethodSymbol method)
            {
                result.AddRange(method.TypeArguments);
            }

            return result;
        }

        private static IEnumerable<INamedTypeSymbol> ContainingTypesOutermostFirst(ISymbol symbol)
        {
            var types = new List<INamedTypeSymbol>();
            for (var type = symbol as INamedTypeSymbol ?? symbol.ContainingType; type != null; type = type.ContainingType)
            {
                types.Insert(0, type);
            }

            return types;
        }

        private static string TrimCrefPrefix(string cref) =>
            cref.Length > 1 && cref[1] == ':' ? cref.Substring(2) : cref;
    }
}
