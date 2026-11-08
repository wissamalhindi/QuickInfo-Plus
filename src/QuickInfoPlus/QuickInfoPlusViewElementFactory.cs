using System.ComponentModel.Composition;
using System.Windows;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace QuickInfoPlus
{
    [Export(typeof(IViewElementFactory))]
    [Name("QuickInfoPlus View Element Factory")]
    [TypeConversion(from: typeof(QuickInfoPlusContent), to: typeof(UIElement))]
    [Order]
    internal sealed class QuickInfoPlusViewElementFactory : IViewElementFactory
    {
        private const string TooltipFormatMap = "tooltip";
        private const string EditorFormatMap = "text";

        [Import]
        internal IClassificationFormatMapService ClassificationFormatMapService { get; set; } = null!;

        [Import]
        internal DocNavigator Navigator { get; set; } = null!;

        [Import]
        internal IViewElementFactoryService ViewElementFactoryService { get; set; } = null!;

        [Import]
        internal IClassificationTypeRegistryService ClassificationTypeRegistryService { get; set; } = null!;

        public TView? CreateViewElement<TView>(ITextView textView, object model) where TView : class
        {
            if (!(model is QuickInfoPlusContent content))
            {
                return null;
            }

            var textProperties = ClassificationFormatMapService.GetClassificationFormatMap(TooltipFormatMap).DefaultTextProperties;
            var editorTextProperties = ClassificationFormatMapService.GetClassificationFormatMap(EditorFormatMap).DefaultTextProperties;
            return QuickInfoView.Create(
                content,
                textProperties,
                editorTextProperties,
                Navigator,
                element => ViewElementFactoryService.CreateViewElement<UIElement>(textView, element),
                name => ClassificationTypeRegistryService.GetClassificationType(name) != null) as TView;
        }
    }
}
