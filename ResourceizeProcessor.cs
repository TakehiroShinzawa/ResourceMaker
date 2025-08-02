using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using ResourceMaker;
using ResourceMaker.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResourceMaker
{
    internal static class ResourceizeProcessor
    {
        public static async Task RunAsync(DTE2 dte)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            string language = dte?.ActiveDocument?.Language;
            string fileName = dte?.ActiveDocument?.Name;

            string editorType = "Unknown";

            if (language == "CSharp")
                editorType = "code";
            else if (fileName?.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase) == true)
                editorType = "xaml";

            Debug.WriteLine($"Called from: {editorType}");

            var selection = dte?.ActiveDocument?.Selection as TextSelection;
            if (selection == null)
                return;

            int cursorLine = selection.ActivePoint.Line;
            EditPoint start = selection.ActivePoint.CreateEditPoint();
            start.StartOfLine(); // 行頭に移動

            EditPoint end = selection.ActivePoint.CreateEditPoint();
            end.EndOfLine(); // 行末に移動

            string lineText = start.GetText(end).TrimStart();

            //フォルダ関係
            Document activeDoc = dte.ActiveDocument;
            ProjectItem item = activeDoc?.ProjectItem;
            Project owningProject = item?.ContainingProject;
            string folder = Path.GetDirectoryName(owningProject.FullName);

            ControlResource(dte,folder,lineText,editorType);

            //var selection = dte?.ActiveDocument?.Selection as TextSelection;
            //if (selection == null)
            //    return;

            //string text = selection.Text;
            //string message = string.IsNullOrWhiteSpace(text)
            //    ? $"← 押されたよ！（{editorType}／選択なし）"
            //    : $"← 押されたよ！（{editorType}／選択: {text}）";

            //selection.Insert(message);
        }

        private static void ControlResource(DTE2 dte, string baseFolderPath, string lineText, string editorType)
        {
            //var langWindow = new LanguageSelectionWindow();
            //langWindow.BaseFolderPath = @"C:\temp\conB2WebApiSimulator\conB2WebApiSimulator\conB2WebApiSimulator";
            //langWindow.EditorType = "code";
            //langWindow.LineText = "await CreateLanguageCodeFoldersAsync(Path.Combine(BaseFolderPath, \"Strings\"));";
            //var result = langWindow.ShowDialog();
            var resWindow = new ResourceEditWindow();
            resWindow.LineText = lineText;
            resWindow.BaseFolderPath = baseFolderPath;
            resWindow.ShowDialog();

        }
    }
}
