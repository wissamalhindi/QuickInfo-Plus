using System.Collections.Generic;
using QuickInfoPlus.Core;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Language.Intellisense;

namespace QuickInfoPlus
{
    internal sealed class QuickInfoPlusContent
    {
        public QuickInfoPlusContent(
            IReadOnlyList<DocBlock> blocks,
            IReadOnlyList<ExceptionEntry> exceptions,
            Project project,
            IAsyncQuickInfoSession session)
        {
            Blocks = blocks;
            Exceptions = exceptions;
            Project = project;
            Session = session;
        }

        public IReadOnlyList<DocBlock> Blocks { get; }

        public IReadOnlyList<ExceptionEntry> Exceptions { get; }

        public Project Project { get; }

        public IAsyncQuickInfoSession Session { get; }
    }
}
