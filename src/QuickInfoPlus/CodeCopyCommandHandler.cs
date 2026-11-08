using System;
using System.ComponentModel.Composition;
using System.Windows;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;

namespace QuickInfoPlus
{
    [Export(typeof(ICommandHandler))]
    [Name("QuickInfoPlus Copy Handler")]
    [ContentType("CSharp")]
    internal sealed class CodeCopyCommandHandler : ICommandHandler<CopyCommandArgs>
    {
        public string DisplayName => "QuickInfoPlus Copy";

        public CommandState GetCommandState(CopyCommandArgs args)
        {
            return CodeSelection.TryGetSelectedText(out _) ? CommandState.Available : CommandState.Unspecified;
        }

        public bool ExecuteCommand(CopyCommandArgs args, CommandExecutionContext executionContext)
        {
            if (!CodeSelection.TryGetSelectedText(out var text))
            {
                return false;
            }

            try
            {
                Clipboard.SetText(text);
            }
            catch (Exception)
            {
            }

            return true;
        }
    }
}
