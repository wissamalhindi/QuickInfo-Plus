namespace QuickInfoPlus.Core
{
    public sealed class DocSections
    {
        public DocSections(bool examples, bool exceptions)
        {
            Examples = examples;
            Exceptions = exceptions;
        }

        public static DocSections All { get; } = new DocSections(examples: true, exceptions: true);

        public bool Examples { get; }

        public bool Exceptions { get; }
    }
}
