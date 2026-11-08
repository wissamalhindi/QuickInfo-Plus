using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;
using System.Windows.Threading;
using QuickInfoPlus.Core;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Adornments;

namespace QuickInfoPlus
{
    internal static class QuickInfoView
    {
        private const string CollapsedArrow = "▸";
        private const string ExpandedArrow = "▾";
        private const double ContentIndent = 14;
        private const double SectionSpacing = 4;
        // Same floor VS uses for the tooltip's maximum width.
        private const double MinimumWidth = 300;
        private const string TextClassification = "text";
        // Segoe MDL2 Assets glyphs: Copy and CheckMark.
        private const string CopyLabel = "\uE8C8";
        private const string CopiedLabel = "\uE73E";

        public static UIElement Create(
            QuickInfoPlusContent content,
            TextRunProperties textProperties,
            TextRunProperties editorTextProperties,
            DocNavigator navigator,
            Func<ClassifiedTextElement, UIElement> render,
            Func<string, bool> isKnownClassification)
        {
            var context = new RenderContext(content, navigator, render, isKnownClassification, textProperties.ForegroundBrush, editorTextProperties);

            var separator = new Border
            {
                Height = 1,
                Margin = new Thickness(0, 4, 0, 0),
                Background = textProperties.ForegroundBrush,
                Opacity = 0.3,
            };

            var root = new StackPanel();
            TextElement.SetForeground(root, textProperties.ForegroundBrush);
            TextElement.SetFontFamily(root, textProperties.Typeface.FontFamily);
            TextElement.SetFontSize(root, textProperties.FontRenderingEmSize);
            root.Children.Add(separator);

            ScrollViewer? exceptionsScroll = null;
            if (content.Exceptions.Count > 0)
            {
                root.Children.Add(CreateSection(
                    "Exceptions",
                    () => SectionExpansionState.IsExceptionsExpanded,
                    value => SectionExpansionState.IsExceptionsExpanded = value,
                    content.Exceptions.SelectMany(e => CreateExceptionEntry(e, context)),
                    double.PositiveInfinity,
                    out exceptionsScroll));
            }

            ScrollViewer? examplesScroll = null;
            var examplesMaxHeight = QuickInfoPlusOptions.ExamplesMaxHeight;
            if (content.Blocks.Count > 0)
            {
                root.Children.Add(CreateSection(
                    "Examples",
                    () => SectionExpansionState.IsExampleExpanded,
                    value => SectionExpansionState.IsExampleExpanded = value,
                    content.Blocks.Select(b => CreateBlock(b, context)),
                    examplesMaxHeight,
                    out examplesScroll));
            }

            var view = new FollowWidthDecorator { Child = root, MinimumWidth = MinimumWidth };
            ScreenFit.Attach(view, exceptionsScroll, examplesScroll, examplesMaxHeight);
            return view;
        }

        // VS gives the tooltip no maximum height and no scrolling, so each section body scrolls on its own: Examples
        // up to the Options height, both within the screen (ScreenFit).
        private static UIElement CreateSection(
            string title,
            Func<bool> isExpanded,
            Action<bool> setExpanded,
            IEnumerable<UIElement> children,
            double maxHeight,
            out ScrollViewer scroll)
        {
            var arrow = new TextBlock
            {
                Text = isExpanded() ? ExpandedArrow : CollapsedArrow,
                Width = ContentIndent,
            };
            var header = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent,
            };
            header.Children.Add(arrow);
            header.Children.Add(new TextBlock(new Bold(new Run(title))));

            var body = new StackPanel
            {
                Margin = new Thickness(ContentIndent, 0, 0, 0),
            };
            foreach (var child in children)
            {
                body.Children.Add(child);
            }

            var shown = new ScrollViewer
            {
                Content = body,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = maxHeight,
            };
            shown.SetResourceReference(FrameworkElement.StyleProperty, VsResourceKeys.ScrollViewerStyleKey);
            scroll = shown;

            shown.Visibility = isExpanded() ? Visibility.Visible : Visibility.Collapsed;

            header.MouseLeftButtonUp += (sender, e) =>
            {
                setExpanded(!isExpanded());
                arrow.Text = isExpanded() ? ExpandedArrow : CollapsedArrow;
                shown.Visibility = isExpanded() ? Visibility.Visible : Visibility.Collapsed;
                e.Handled = true;
            };

            var section = new StackPanel
            {
                Margin = new Thickness(0, SectionSpacing, 0, 0),
            };
            section.Children.Add(header);
            section.Children.Add(shown);
            return section;
        }

        private static IEnumerable<UIElement> CreateExceptionEntry(ExceptionEntry entry, RenderContext context)
        {
            var inlines = entry.Type.AsEnumerable();
            var remaining = entry.Description.AsEnumerable();
            var first = entry.Description.FirstOrDefault();
            if (first != null && first.Kind == DocBlockKind.Text)
            {
                inlines = inlines.Concat(new[] { new DocInline(DocInlineKind.Text, " – ") }).Concat(first.Inlines);
                remaining = remaining.Skip(1);
            }

            yield return CreateText(inlines, context, leftMargin: 0);
            foreach (var block in remaining)
            {
                yield return block.Kind == DocBlockKind.Code
                    ? CreateCode(block, leftMargin: 16, context)
                    : CreateText(block.Inlines, context, leftMargin: 16);
            }
        }

        private static UIElement CreateBlock(DocBlock block, RenderContext context)
        {
            return block.Kind == DocBlockKind.Code
                ? CreateCode(block, leftMargin: 0, context)
                : CreateText(block.Inlines, context, leftMargin: 0);
        }

        private static UIElement CreateCode(DocBlock block, double leftMargin, RenderContext context)
        {
            var code = block.Text.Replace("\n", Environment.NewLine);
            var editor = context.EditorTextProperties;

            var bar = new Border
            {
                Width = 3,
                Background = context.Foreground,
                Opacity = 0.4,
                Margin = new Thickness(0, 0, 8, 0),
            };

            var text = new TextBox
            {
                Text = code,
                IsReadOnly = true,
                IsReadOnlyCaretVisible = false,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = context.Foreground,
                FontFamily = editor.Typeface.FontFamily,
                FontSize = editor.FontRenderingEmSize,
                Padding = new Thickness(0),
                TextWrapping = TextWrapping.NoWrap,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };
            ContextMenuService.SetIsEnabled(text, false);
            text.PreviewMouseRightButtonDown += (sender, e) => e.Handled = true;
            text.PreviewMouseRightButtonUp += (sender, e) => e.Handled = true;
            CodeSelection.Track(text);

            var copyButton = new TextBlock
            {
                Text = CopyLabel,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 14,
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent,
                Opacity = 0.7,
                Padding = new Thickness(2),
                Margin = new Thickness(12, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Visibility = Visibility.Hidden,
            };
            copyButton.MouseEnter += (sender, e) => copyButton.Opacity = 1.0;
            copyButton.MouseLeave += (sender, e) => copyButton.Opacity = 0.7;
            copyButton.MouseLeftButtonUp += (sender, e) =>
            {
                try
                {
                    Clipboard.SetText(code);
                    copyButton.Text = CopiedLabel;
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                    timer.Tick += (s, args) =>
                    {
                        timer.Stop();
                        copyButton.Text = CopyLabel;
                    };
                    timer.Start();
                }
                catch (Exception)
                {
                }

                e.Handled = true;
            };

            var codeScroll = new ScrollViewer
            {
                Content = text,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };
            codeScroll.SetResourceReference(FrameworkElement.StyleProperty, VsResourceKeys.ScrollViewerStyleKey);

            var grid = new Grid
            {
                Margin = new Thickness(leftMargin, 4, 0, 2),
                Background = Brushes.Transparent,
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(bar, 0);
            Grid.SetColumn(codeScroll, 1);
            Grid.SetColumn(copyButton, 2);
            grid.Children.Add(bar);
            grid.Children.Add(codeScroll);
            grid.Children.Add(copyButton);
            grid.MouseEnter += (sender, e) => copyButton.Visibility = Visibility.Visible;
            grid.MouseLeave += (sender, e) => copyButton.Visibility = Visibility.Hidden;

            // The text box would swallow the mouse wheel: Shift+wheel scrolls the code sideways, the plain wheel goes
            // on to the section's scroll area.
            grid.PreviewMouseWheel += (sender, e) =>
            {
                e.Handled = true;
                if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
                {
                    codeScroll.ScrollToHorizontalOffset(codeScroll.HorizontalOffset - e.Delta);
                    return;
                }

                if (VisualTreeHelper.GetParent(grid) is UIElement parent)
                {
                    parent.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                    {
                        RoutedEvent = UIElement.MouseWheelEvent,
                        Source = grid,
                    });
                }
            };
            return grid;
        }

        private static UIElement CreateText(IEnumerable<DocInline> inlines, RenderContext context, double leftMargin)
        {
            var element = new ClassifiedTextElement(inlines.Select(i => CreateRun(i, context)));
            return new Border
            {
                Child = context.Render(element),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(leftMargin, 2, 0, 0),
            };
        }

        private static ClassifiedTextRun CreateRun(DocInline inline, RenderContext context)
        {
            var classification = context.IsKnownClassification(inline.Classification) ? inline.Classification : TextClassification;
            var target = inline.Target;
            switch (inline.Kind)
            {
                case DocInlineKind.SymbolLink when target != null:
                    return new ClassifiedTextRun(classification, inline.Text, () => context.Navigator.GoToSymbol(context.Content, target), inline.Hint ?? inline.Text, ClassifiedTextRunStyle.Plain);
                case DocInlineKind.Link when target != null:
                    return new ClassifiedTextRun(TextClassification, inline.Text, () => context.Navigator.OpenUrl(context.Content, target), inline.Hint ?? target, ClassifiedTextRunStyle.Plain);
                default:
                    return new ClassifiedTextRun(classification, inline.Text);
            }
        }

        private sealed class RenderContext
        {
            public RenderContext(
                QuickInfoPlusContent content,
                DocNavigator navigator,
                Func<ClassifiedTextElement, UIElement> render,
                Func<string, bool> isKnownClassification,
                Brush foreground,
                TextRunProperties editorTextProperties)
            {
                Content = content;
                Navigator = navigator;
                Render = render;
                IsKnownClassification = isKnownClassification;
                Foreground = foreground;
                EditorTextProperties = editorTextProperties;
            }

            public Brush Foreground { get; }

            public TextRunProperties EditorTextProperties { get; }

            public QuickInfoPlusContent Content { get; }

            public DocNavigator Navigator { get; }

            public Func<ClassifiedTextElement, UIElement> Render { get; }

            public Func<string, bool> IsKnownClassification { get; }
        }
    }
}
