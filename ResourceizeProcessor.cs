using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using ResourceMaker;
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

            string lineText = start.GetText(end);

            //フォルダ関係
            Project project = ((Array)dte.ActiveSolutionProjects).GetValue(0) as Project;
            string folder = Path.GetDirectoryName(project.FullName);

            await ControlResourceAsync(dte,folder,lineText,editorType);

            //var selection = dte?.ActiveDocument?.Selection as TextSelection;
            //if (selection == null)
            //    return;

            //string text = selection.Text;
            //string message = string.IsNullOrWhiteSpace(text)
            //    ? $"← 押されたよ！（{editorType}／選択なし）"
            //    : $"← 押されたよ！（{editorType}／選択: {text}）";

            //selection.Insert(message);
        }

        private static async Task ControlResourceAsync(DTE2 dte, string baseFolderPath, string lineText, string editorType)
        {
            string stringsFolderPath = Path.Combine(baseFolderPath, "Strings");

            await Task.Run(() =>
            {
                if (!Directory.Exists(stringsFolderPath))
                {
                    Directory.CreateDirectory(stringsFolderPath);
                    // ログ出力や初期ファイル生成などもここに追加可能
                }
            });
            var window = new LanguageSelectionWindow();
            window.ShowDialog(); // または Show() でも可

        }
    }
}
