using System;
using System.Windows;
using System.Windows.Controls;

namespace QuickInfoPlus
{
    // The tooltip never gets keyboard focus, so Ctrl+C reaches the editor; this remembers which tooltip code box
    // currently has a selection so the editor's Copy command can copy it instead.
    internal static class CodeSelection
    {
        private static WeakReference<TextBox>? _current;

        public static void Track(TextBox textBox)
        {
            textBox.SelectionChanged += (sender, e) =>
            {
                if (textBox.SelectionLength > 0)
                {
                    _current = new WeakReference<TextBox>(textBox);
                }
                else
                {
                    Clear(textBox);
                }
            };
            textBox.IsVisibleChanged += (sender, e) =>
            {
                if (!textBox.IsVisible)
                {
                    Clear(textBox);
                }
            };
            textBox.Unloaded += (sender, e) => Clear(textBox);
        }

        public static bool TryGetSelectedText(out string text)
        {
            text = "";
            if (_current == null || !_current.TryGetTarget(out var textBox))
            {
                return false;
            }

            if (!textBox.IsVisible || textBox.SelectionLength == 0)
            {
                _current = null;
                return false;
            }

            text = textBox.SelectedText;
            return true;
        }

        private static void Clear(TextBox textBox)
        {
            if (_current != null && _current.TryGetTarget(out var current) && ReferenceEquals(current, textBox))
            {
                _current = null;
            }
        }
    }
}
