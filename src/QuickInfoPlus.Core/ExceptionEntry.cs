using System.Collections.Generic;

namespace QuickInfoPlus.Core
{
    public sealed class ExceptionEntry
    {
        public ExceptionEntry(IReadOnlyList<DocInline> type, IReadOnlyList<DocBlock> description)
        {
            Type = type;
            Description = description;
        }

        public IReadOnlyList<DocInline> Type { get; }

        public IReadOnlyList<DocBlock> Description { get; }
    }
}
