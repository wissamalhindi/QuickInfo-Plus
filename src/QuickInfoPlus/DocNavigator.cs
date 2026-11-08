using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Threading;

namespace QuickInfoPlus
{
    [Export]
    internal sealed class DocNavigator
    {
        private readonly VisualStudioWorkspace _workspace;
        private readonly JoinableTaskContext _joinableTaskContext;

        [ImportingConstructor]
        public DocNavigator(VisualStudioWorkspace workspace, JoinableTaskContext joinableTaskContext)
        {
            _workspace = workspace;
            _joinableTaskContext = joinableTaskContext;
        }

        public void OpenUrl(QuickInfoPlusContent content, string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return;
            }

            var factory = _joinableTaskContext.Factory;
            _ = factory.RunAsync(async () =>
            {
                try
                {
                    // Close the tooltip ourselves, like code links do: a browser opening in the background would
                    // otherwise leave it open over the editor.
                    await factory.SwitchToMainThreadAsync();
                    await content.Session.DismissAsync();
                    Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
                }
                catch (Exception)
                {
                }
            });
        }

        public void GoToSymbol(QuickInfoPlusContent content, string documentationCommentId)
        {
            var factory = _joinableTaskContext.Factory;
            _ = factory.RunAsync(async () =>
            {
                try
                {
                    var project = _workspace.CurrentSolution.GetProject(content.Project.Id) ?? content.Project;
                    var compilation = await project.GetCompilationAsync().ConfigureAwait(false);
                    var symbol = compilation == null ? null : DocumentationCommentId.GetFirstSymbolForDeclarationId(documentationCommentId, compilation);
                    if (symbol == null)
                    {
                        return;
                    }

                    await factory.SwitchToMainThreadAsync();
                    await content.Session.DismissAsync();
                    await _workspace.TryGoToDefinitionAsync(symbol, project, CancellationToken.None);
                }
                catch (Exception)
                {
                }
            });
        }
    }
}
