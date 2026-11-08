using System;
using QuickInfoPlus.Core;
using Xunit;

namespace QuickInfoPlus.Core.Tests
{
    public class SectionLimitsTests
    {
        [Theory]
        [InlineData(100, 100)]
        [InlineData(400, 400)]
        [InlineData(1000, 1000)]
        [InlineData(99, 100)]
        [InlineData(0, 100)]
        [InlineData(-5, 100)]
        [InlineData(1001, 1000)]
        [InlineData(int.MaxValue, 1000)]
        public void ClampExamplesHeight_KeepsValueWithinRange(int value, int expected)
        {
            Assert.Equal(expected, SectionLimits.ClampExamplesHeight(value));
        }

        [Fact]
        public void DefaultExamplesHeight_Is300()
        {
            Assert.Equal(300, SectionLimits.DefaultExamplesHeight);
        }

        private const double None = double.PositiveInfinity;

        [Theory]
        [InlineData(800, 200, 500, 300, None, 300)] // fits: unchanged
        [InlineData(400, 150, 500, 300, None, 250)] // Examples shrinks first
        [InlineData(400, 350, 500, 300, 200, 200)] // Examples keeps half, Exceptions scrolls
        [InlineData(400, 600, 80, 300, 320, 80)] // short Examples keeps its full height
        [InlineData(400, 600, 0, 300, 400, 300)] // Examples collapsed: Exceptions gets all the room; Examples keeps
                                                 // its Options height, so expanding it resizes the tooltip and refits
        [InlineData(400, 0, 900, 1000, None, 400)] // Exceptions collapsed: Examples gets all the room
        [InlineData(-50, 100, 100, 300, 0, 0)] // no room at all
        public void FitToRoom_SharesTheRoom(
            double room, double exceptionsFull, double examplesFull, int examplesOption, double exceptionsMax, double examplesMax)
        {
            var heights = SectionLimits.FitToRoom(room, exceptionsFull, examplesFull, examplesOption);

            Assert.Equal(exceptionsMax, heights.ExceptionsMax);
            Assert.Equal(examplesMax, heights.ExamplesMax);
        }

        [Theory]
        [InlineData(300, 1000, 1000, 1000)]
        [InlineData(500, 499, 2, 100)]
        [InlineData(120, 50, 900, 100)]
        public void FitToRoom_ShownHeightsNeverExceedRoom(double room, double exceptionsFull, double examplesFull, int examplesOption)
        {
            var heights = SectionLimits.FitToRoom(room, exceptionsFull, examplesFull, examplesOption);

            var shown = Math.Min(exceptionsFull, heights.ExceptionsMax)
                + Math.Min(Math.Min(examplesFull, examplesOption), heights.ExamplesMax);
            Assert.True(shown <= room, $"shown {shown} > room {room}");
        }
    }
}
