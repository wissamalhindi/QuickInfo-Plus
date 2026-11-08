namespace QuickInfoPlus
{
    // Expanded/collapsed state of each section, shared by all tooltips until Visual Studio closes.
    internal static class SectionExpansionState
    {
        public static bool IsExampleExpanded { get; set; }

        public static bool IsExceptionsExpanded { get; set; }
    }
}
