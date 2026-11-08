using System.ComponentModel;
using System.Windows;
using Microsoft.VisualStudio.Shell;
using QuickInfoPlus.Core;

namespace QuickInfoPlus
{
    // Shows OptionsForm instead of a property grid; the properties stay for VS's settings import/export.
    public sealed class GeneralOptionsPage : UIElementDialogPage
    {
        private OptionsForm? _form;

        [Category("Sections")]
        [DisplayName("Show Exceptions section")]
        [Description("Show the <exception> types and descriptions of the hovered C# symbol in Quick Info.")]
        [DefaultValue(true)]
        public bool ShowExceptions { get; set; } = true;

        [Category("Sections")]
        [DisplayName("Show Examples section")]
        [Description("Show the <example> documentation of the hovered C# symbol in Quick Info.")]
        [DefaultValue(true)]
        public bool ShowExamples { get; set; } = true;

        [Category("Examples section")]
        [DisplayName("Maximum height (px)")]
        [Description("Maximum height of the Examples section (100-1000). Longer content scrolls.")]
        [DefaultValue(SectionLimits.DefaultExamplesHeight)]
        public int ExamplesMaxHeight { get; set; } = SectionLimits.DefaultExamplesHeight;

        protected override UIElement Child => _form ??= new OptionsForm();

        protected override void OnActivate(CancelEventArgs e)
        {
            base.OnActivate(e);
            ((OptionsForm)Child).Show(ShowExceptions, ShowExamples, ExamplesMaxHeight);
        }

        protected override void OnApply(PageApplyEventArgs e)
        {
            if (e.ApplyBehavior == ApplyKind.Apply && _form != null)
            {
                ShowExceptions = _form.ShowExceptions;
                ShowExamples = _form.ShowExamples;
                ExamplesMaxHeight = _form.ExamplesMaxHeight;
            }

            base.OnApply(e);
        }

        public override void LoadSettingsFromStorage()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            QuickInfoPlusOptions.Load();
            ShowExamples = QuickInfoPlusOptions.ShowExamples;
            ShowExceptions = QuickInfoPlusOptions.ShowExceptions;
            ExamplesMaxHeight = QuickInfoPlusOptions.ExamplesMaxHeight;
        }

        public override void SaveSettingsToStorage()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            QuickInfoPlusOptions.Save(ShowExamples, ShowExceptions, ExamplesMaxHeight);
            ExamplesMaxHeight = QuickInfoPlusOptions.ExamplesMaxHeight;
        }
    }
}
