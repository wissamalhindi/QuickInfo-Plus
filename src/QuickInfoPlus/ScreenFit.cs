using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using QuickInfoPlus.Core;

namespace QuickInfoPlus
{
    // Keeps the tooltip within the screen: shares the room the monitor has left between the two section bodies.
    internal static class ScreenFit
    {
        private const double Gap = 8;
        private const double Tolerance = 0.5;
        private const uint MonitorDefaultToNearest = 2;

        public static void Attach(FrameworkElement tooltipContent, ScrollViewer? exceptions, ScrollViewer? examples, int examplesOption)
        {
            HwndSource? source = null;
            FrameworkElement? tooltip = null;

            void Fit(object sender, EventArgs e)
            {
                try
                {
                    if (source == null || tooltip == null || source.CompositionTarget == null)
                    {
                        return;
                    }

                    var workHeight = WorkAreaHeight(source);
                    if (workHeight == null)
                    {
                        return;
                    }

                    var shown = ShownHeight(exceptions) + ShownHeight(examples);
                    var room = workHeight.Value - Gap - (tooltip.ActualHeight - shown);
                    var heights = SectionLimits.FitToRoom(room, FullHeight(exceptions), FullHeight(examples), examplesOption);
                    SetMaxHeight(exceptions, heights.ExceptionsMax);
                    SetMaxHeight(examples, heights.ExamplesMax);
                }
                catch (Exception)
                {
                }
            }

            void OnLoaded(object sender, RoutedEventArgs e)
            {
                source = PresentationSource.FromVisual(tooltipContent) as HwndSource;
                tooltip = source?.RootVisual as FrameworkElement;
                if (tooltip == null)
                {
                    return;
                }

                tooltip.SizeChanged += Fit;
                tooltipContent.SizeChanged += Fit;
                Fit(sender, e);
            }

            void OnUnloaded(object sender, RoutedEventArgs e)
            {
                if (tooltip != null)
                {
                    tooltip.SizeChanged -= Fit;
                }

                tooltipContent.SizeChanged -= Fit;
                source = null;
                tooltip = null;
            }

            tooltipContent.Loaded += OnLoaded;
            tooltipContent.Unloaded += OnUnloaded;
        }

        // Usable height (taskbar excluded) of the monitor the tooltip is on, in WPF device-independent pixels.
        private static double? WorkAreaHeight(HwndSource source)
        {
            var monitor = MonitorFromWindow(source.Handle, MonitorDefaultToNearest);
            var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
            if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
            {
                return null;
            }

            var devicePixels = info.WorkArea.Bottom - info.WorkArea.Top;
            return source.CompositionTarget.TransformFromDevice.Transform(new Vector(0, devicePixels)).Y;
        }

        private static double ShownHeight(ScrollViewer? scroll)
        {
            return scroll != null && scroll.Visibility == Visibility.Visible ? scroll.ActualHeight : 0;
        }

        private static double FullHeight(ScrollViewer? scroll)
        {
            return scroll != null && scroll.Visibility == Visibility.Visible && scroll.Content is UIElement body
                ? body.DesiredSize.Height
                : 0;
        }

        // Only real changes, so a new limit cannot set off an endless resize.
        private static void SetMaxHeight(ScrollViewer? scroll, double value)
        {
            if (scroll == null)
            {
                return;
            }

            var current = scroll.MaxHeight;
            if (double.IsPositiveInfinity(value) ? double.IsPositiveInfinity(current) : Math.Abs(current - value) <= Tolerance)
            {
                return;
            }

            scroll.MaxHeight = value;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int Size;
            public Rect Monitor;
            public Rect WorkArea;
            public uint Flags;
        }
    }
}
