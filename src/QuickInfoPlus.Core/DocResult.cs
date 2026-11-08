using System.Collections.Generic;

namespace QuickInfoPlus.Core
{
    public sealed class DocResult
    {
        public DocResult(IReadOnlyList<DocBlock> blocks, IReadOnlyList<ExceptionEntry> exceptions, int spanStart, int spanLength)
        {
            Blocks = blocks;
            Exceptions = exceptions;
            SpanStart = spanStart;
            SpanLength = spanLength;
        }

        public IReadOnlyList<DocBlock> Blocks { get; }

        public IReadOnlyList<ExceptionEntry> Exceptions { get; }

        public int SpanStart { get; }

        public int SpanLength { get; }
    }
}
