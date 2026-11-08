using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QuickInfoPlus.Core;

namespace QuickInfoPlus
{
    // The General options page: checkboxes in the tooltip's order and the Examples height.
    internal sealed class OptionsForm : UserControl
    {
        private readonly CheckBox _showExceptions;
        private readonly CheckBox _showExamples;
        private readonly TextBox _examplesMaxHeight;
        private int _lastHeight = SectionLimits.DefaultExamplesHeight;

        public OptionsForm()
        {
            _showExceptions = new CheckBox { Content = "Show Exceptions section", Margin = new Thickness(12, 4, 0, 0) };
            _showExamples = new CheckBox { Content = "Show Examples section", Margin = new Thickness(12, 4, 0, 0) };
            _examplesMaxHeight = new TextBox { Width = 60, Margin = new Thickness(6, 0, 6, 0), VerticalContentAlignment = VerticalAlignment.Center };
            _examplesMaxHeight.PreviewTextInput += (sender, e) => e.Handled = !IsDigits(e.Text);
            DataObject.AddPastingHandler(_examplesMaxHeight, (sender, e) =>
            {
                if (!(e.DataObject.GetData(typeof(string)) is string text) || !IsDigits(text))
                {
                    e.CancelCommand();
                }
            });

            var heightRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 4, 0, 0) };
            heightRow.Children.Add(new TextBlock { Text = "Maximum height (px):", VerticalAlignment = VerticalAlignment.Center });
            heightRow.Children.Add(_examplesMaxHeight);
            heightRow.Children.Add(new TextBlock
            {
                Text = $"({SectionLimits.MinExamplesHeight}–{SectionLimits.MaxExamplesHeight})",
                VerticalAlignment = VerticalAlignment.Center,
            });

            var panel = new StackPanel { Margin = new Thickness(8) };
            panel.Children.Add(Header("Sections", top: 0));
            panel.Children.Add(_showExceptions);
            panel.Children.Add(_showExamples);
            panel.Children.Add(Header("Examples section", top: 16));
            panel.Children.Add(heightRow);
            Content = panel;

            // Like VS's own (Roslyn) option pages: no background of our own and system text colors, which the Options
            // dialog maps to the current VS theme.
            Resources.Add(typeof(TextBlock), ThemedStyle(typeof(TextBlock), TextBlock.ForegroundProperty));
            Resources.Add(typeof(CheckBox), ThemedStyle(typeof(CheckBox), Control.ForegroundProperty));
            Resources.Add(typeof(TextBox), ThemedStyle(typeof(TextBox), Control.ForegroundProperty));
        }

        private static Style ThemedStyle(System.Type type, DependencyProperty foreground)
        {
            var style = new Style(type);
            style.Setters.Add(new Setter(foreground, new DynamicResourceExtension(SystemColors.WindowTextBrushKey)));
            return style;
        }

        public bool ShowExceptions => _showExceptions.IsChecked == true;

        public bool ShowExamples => _showExamples.IsChecked == true;

        // An empty or unreadable box keeps the last value; out-of-range values are clamped.
        public int ExamplesMaxHeight =>
            int.TryParse(_examplesMaxHeight.Text, out var value) ? SectionLimits.ClampExamplesHeight(value) : _lastHeight;

        public void Show(bool showExceptions, bool showExamples, int examplesMaxHeight)
        {
            _showExceptions.IsChecked = showExceptions;
            _showExamples.IsChecked = showExamples;
            _lastHeight = SectionLimits.ClampExamplesHeight(examplesMaxHeight);
            _examplesMaxHeight.Text = _lastHeight.ToString();
        }

        private static TextBlock Header(string text, double top)
        {
            return new TextBlock { Text = text, FontWeight = FontWeights.Bold, Margin = new Thickness(0, top, 0, 0) };
        }

        private static bool IsDigits(string text)
        {
            foreach (var c in text)
            {
                if (c < '0' || c > '9')
                {
                    return false;
                }
            }

            return text.Length > 0;
        }
    }
}
