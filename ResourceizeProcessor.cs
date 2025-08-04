using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using ResourceMaker;
using ResourceMaker.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResourceMaker
{
    internal static class ResourceizeProcessor
    {
        public static async Task RunAsync(DTE2 dte, IServiceProvider serviceProvider)
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

            string originalLine = start.GetText(end);

            string lineText = originalLine.TrimStart();
            string indent = originalLine.Substring(0, originalLine.Length - lineText.Length); // 行頭の空白だけを抽出

            //フォルダ関係
            Document activeDoc = dte.ActiveDocument;
            ProjectItem item = activeDoc?.ProjectItem;
            Project owningProject = item?.ContainingProject;
            string folder = Path.GetDirectoryName(owningProject.FullName);

            var feedback =  ControlResource(dte,folder,lineText,editorType, serviceProvider);

            if( !string.IsNullOrEmpty(feedback))
            {
                start.Delete(end); // 元の行のテキストを削除
                start.Insert(indent + feedback); // 新しいテキストを挿入

            }
        }

        private static String ControlResource(DTE2 dte, 
            string baseFolderPath, string lineText, string editorType, IServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            //var langWindow = new LanguageSelectionWindow();
            //langWindow.BaseFolderPath = @"C:\temp\conB2WebApiSimulator\conB2WebApiSimulator\conB2WebApiSimulator";
            //langWindow.EditorType = "code";
            //langWindow.LineText = "await CreateLanguageCodeFoldersAsync(Path.Combine(BaseFolderPath, \"Strings\"));";
            //var result = langWindow.ShowDialog();

            int lcid = dte.LocaleID;
            CultureInfo culture = new CultureInfo(lcid);

            var resWindow = new ResourceEditWindow(serviceProvider);
            resWindow.LangCulture = culture.ToString();
            resWindow.LineText = lineText;
            resWindow.EditorType = editorType;
            resWindow.BaseFolderPath = baseFolderPath;
            resWindow.ShowDialog();
            return resWindow.FeedbackText;

        }
    }
}
