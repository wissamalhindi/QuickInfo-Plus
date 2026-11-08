using System;

namespace QuickInfoPlus.Core
{
    public static class SectionLimits
    {
        public const int MinExamplesHeight = 100;
        public const int MaxExamplesHeight = 1000;
        public const int DefaultExamplesHeight = 300;

        public static int ClampExamplesHeight(int value) => Math.Min(MaxExamplesHeight, Math.Max(MinExamplesHeight, value));

        // Shares the room left on the screen between the two section bodies. Full heights are 0 for a collapsed or
        // absent section. Examples shrinks first but keeps at least half the room; then Exceptions scrolls.
        public static SectionHeights FitToRoom(double room, double exceptionsFull, double examplesFull, int examplesOption)
        {
            room = Math.Max(0, room);
            var wanted = Math.Min(examplesOption, examplesFull);
            if (exceptionsFull + wanted <= room)
            {
                return new SectionHeights(double.PositiveInfinity, examplesOption);
            }

            var examples = Math.Min(wanted, Math.Max(room - exceptionsFull, room / 2));
            var exceptions = room - examples;
            // A collapsed Examples keeps its Options height: expanding it then resizes the tooltip, which refits.
            return new SectionHeights(
                exceptionsFull <= exceptions ? double.PositiveInfinity : exceptions,
                examplesFull > 0 ? examples : examplesOption);
        }
    }

    public readonly struct SectionHeights
    {
        public SectionHeights(double exceptionsMax, double examplesMax)
        {
            ExceptionsMax = exceptionsMax;
            ExamplesMax = examplesMax;
        }

        // double.PositiveInfinity means no cap.
        public double ExceptionsMax { get; }

        public double ExamplesMax { get; }
    }
}
