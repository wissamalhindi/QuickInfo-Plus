using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using QuickInfoPlus.Core;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

namespace QuickInfoPlus
{
    [Export(typeof(IAsyncQuickInfoSourceProvider))]
    [Name("QuickInfoPlus Quick Info Source")]
    [ContentType("CSharp")]
    [Order(After = "RoslynQuickInfoProvider")]
    internal sealed class QuickInfoPlusSourceProvider : IAsyncQuickInfoSourceProvider
    {
        [Import]
        internal JoinableTaskContext JoinableTaskContext { get; set; } = null!;

        public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
        {
            return textBuffer.Properties.GetOrCreateSingletonProperty(() => new QuickInfoPlusSource(textBuffer, JoinableTaskContext.Factory));
        }
    }

    internal sealed class QuickInfoPlusSource : IAsyncQuickInfoSource
    {
        private readonly ITextBuffer _textBuffer;
        private readonly JoinableTaskFactory _joinableTaskFactory;

        public QuickInfoPlusSource(ITextBuffer textBuffer, JoinableTaskFactory joinableTaskFactory)
        {
            _textBuffer = textBuffer;
            _joinableTaskFactory = joinableTaskFactory;
        }

        public async Task<QuickInfoItem?> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken)
        {
            try
            {
                await QuickInfoPlusOptions.EnsureLoadedAsync(_joinableTaskFactory, cancellationToken);
                var sections = new DocSections(QuickInfoPlusOptions.ShowExamples, QuickInfoPlusOptions.ShowExceptions);
                if (!sections.Examples && !sections.Exceptions)
                {
                    return null;
                }

                var triggerPoint = session.GetTriggerPoint(_textBuffer.CurrentSnapshot);
                if (triggerPoint == null)
                {
                    return null;
                }

                var snapshot = triggerPoint.Value.Snapshot;
                var document = snapshot.GetOpenDocumentInCurrentContextWithChanges();
                if (document == null)
                {
                    return null;
                }

                var result = await DocLookup.FindAsync(document, triggerPoint.Value.Position, sections, cancellationToken).ConfigureAwait(false);
                if (result == null)
                {
                    return null;
                }

                var span = snapshot.CreateTrackingSpan(result.SpanStart, result.SpanLength, SpanTrackingMode.EdgeInclusive);
                return new QuickInfoItem(span, new QuickInfoPlusContent(result.Blocks, result.Exceptions, document.Project, session));
            }
            catch (Exception e) when (!(e is OperationCanceledException))
            {
                return null;
            }
        }

        public void Dispose()
        {
        }
    }
}
