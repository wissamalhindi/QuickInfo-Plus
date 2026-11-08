using System;
using System.Windows;
using System.Windows.Controls;

namespace QuickInfoPlus
{
    // Lays its child out at the width the tooltip already has from VS's own content (signature, summary, ...)
    // instead of widening the tooltip: it asks for only MinimumWidth while measuring, then re-measures its child
    // at the width it is actually given.
    internal sealed class FollowWidthDecorator : Decorator
    {
        private double _arrangedWidth = double.NaN;

        public double MinimumWidth { get; set; }

        protected override Size MeasureOverride(Size constraint)
        {
            if (Child == null)
            {
                return default;
            }

            var width = double.IsNaN(_arrangedWidth) ? MinimumWidth : Math.Max(_arrangedWidth, MinimumWidth);
            width = Math.Min(width, constraint.Width);
            Child.Measure(new Size(width, constraint.Height));
            return new Size(Math.Min(MinimumWidth, constraint.Width), Child.DesiredSize.Height);
        }

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            if (Child == null)
            {
                return arrangeSize;
            }

            if (double.IsNaN(_arrangedWidth) || Math.Abs(arrangeSize.Width - _arrangedWidth) > 0.5)
            {
                _arrangedWidth = arrangeSize.Width;
                InvalidateMeasure();
            }

            Child.Arrange(new Rect(arrangeSize));
            return arrangeSize;
        }
    }
}
