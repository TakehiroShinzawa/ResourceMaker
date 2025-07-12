using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;

[Export(typeof(ICommandHandler))]
[ContentType("CSharp")] // XAMLなら "XAML"
[TextViewRole(PredefinedTextViewRoles.Editable)]
internal class ResourceizeCommandHandler : ICommandHandler<TypeCharCommandArgs>
{
    public string DisplayName => "Resourceize MEF Command";

    public bool ExecuteCommand(TypeCharCommandArgs args, CommandExecutionContext executionContext)
    {
        var caret = args.TextView.Caret.Position.BufferPosition;
        args.TextView.TextBuffer.Insert(caret.Position, " ← 押されたよ（MEF動作）");
        return true;
    }

    public CommandState GetCommandState(TypeCharCommandArgs args)
    {
        return CommandState.Available;
    }
}