using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
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
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;

namespace ResourceMaker
{
    internal static class ResourceizeProcessor
    {
        private static string devType = string.Empty;
        private static string cachedProjectPath = string.Empty;

        public static async Task RunAsync(DTE2 dte, System.IServiceProvider serviceProvider)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var activeDoc = dte?.ActiveDocument ?? null;
            string language = activeDoc?.Language;
            string fileName = activeDoc?.Name;

            string editorType = "code";

            if (language == "CSharp")
                editorType = "code";
            else if (fileName?.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase) == true)
                editorType = "xaml";

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
            ProjectItem item = activeDoc?.ProjectItem;
            Project owningProject = item?.ContainingProject;
            var projectFile = owningProject.FullName;
            string folder = Path.GetDirectoryName(projectFile);

            int lcid = dte.LocaleID;
            CultureInfo culture = new CultureInfo(lcid);

            //システムキャッシュ
            var shellSettingsManager = new ShellSettingsManager(serviceProvider); // ← ServiceProvider を渡すか取得する
            var store = shellSettingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

            if (!store.CollectionExists("ResourceMaker"))
                store.CreateCollection("ResourceMaker");

            //プロジェクトキャッシュ
            if (!store.PropertyExists("ResourceMaker", "LastProject"))
                store.SetString("ResourceMaker", "LastProject", "");
            
            cachedProjectPath = store.GetString("ResourceMaker", "LastProject");

            if (!store.PropertyExists("ResourceMaker", "LastDevType"))
                store.SetString("ResourceMaker", "LastDevType", "");

            devType = store.GetString("ResourceMaker", "LastDevType");

            //loader情報
            if (!store.PropertyExists("ResourceMaker", "AccessMethod"))
                store.SetString("ResourceMaker", "AccessMethod", "loader.GetString");
#if DEBUG
            //cachedProjectPath = "";    ///ToDo
#endif  
            if (folder != cachedProjectPath)
            {
                var resLanguage = new LanguageSelectionWindow();
                resLanguage.DevelopType = "";
                resLanguage.LangCulture = culture.ToString();
                resLanguage.BaseFolderPath = folder;
                var result = resLanguage.ShowDialog();
                if (result == false)
                    return;
                devType = resLanguage.DevelopType;
                cachedProjectPath = folder;
                store.SetString("ResourceMaker", "LastProject", cachedProjectPath);
                store.SetString("ResourceMaker", "LastDevType", devType);
            }

            XElement element = null;
            //xamlをひっかける
            if (editorType == "xaml")
                element = ExtractTextBoxXml(start);

            //リソースエディタの展開
            var resWindow = new ResourceEditWindow(serviceProvider);
            resWindow.LangCulture = culture.ToString();
            resWindow.AccessMethod = store.GetString("ResourceMaker", "AccessMethod");

            resWindow.LineText = lineText;
            resWindow.EditorType = editorType;
            resWindow.DevelopType = devType;
            resWindow.ProjectName = owningProject.Name;
            resWindow.element = element;
            resWindow.BaseFolderPath = folder;
            if( string.IsNullOrEmpty(resWindow.DevelopType))
            {
                store.SetString("ResourceMaker", "LastProject", "");
                store.SetString("ResourceMaker", "LastDevType", "");
                ResourceCacheController.Clear();
                return;
            }
            if (resWindow.FeedbackText == "NG")
                return;

            resWindow.ShowDialog();
            var feedback = resWindow.FeedbackText;

            if (!string.IsNullOrEmpty(feedback))
            {
                if (feedback == " ")
                    end.CharRight(); // 改行文字も含める（必要なら）
                start.Delete(end); // 元の行のテキストを削除
                if (feedback != " ")
                    start.Insert(indent + feedback); // 新しいテキストを挿入

            }
        }
        public static bool IsVsixProject(Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (project?.Properties == null)
                return false;

            try
            {
                foreach (Property prop in project.Properties)
                {
                    Debug.WriteLine(prop.Name);
                    if (prop?.Name?.StartsWith("VsixProjectExtender.") == true)
                        return true;
                }
            }
            catch { }

            return false;
        }


        public static XElement ExtractTextBoxXml(EditPoint startPoint)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            StringBuilder builder = new StringBuilder();
            EditPoint cursor = startPoint.CreateEditPoint();
            int currentLine = cursor.Line;
            string lineText = string.Empty;
            bool foundStart = false;
            var x = "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";
            try
            {
                lineText = (cursor.GetLines(currentLine, currentLine + 1)).Trim();
                if (lineText.StartsWith("<") && lineText.EndsWith(">"))
                {
                    int spacePos = lineText.IndexOf(' ');
                    builder.Append(lineText.Substring(0, spacePos));
                    builder.Append($" {x}");
                    builder.AppendLine(lineText.Substring(spacePos));

                }
                else
                {
                    // 上に向かって `<TextBox` を探す
                    while (currentLine >= 1)
                    {
                        lineText = cursor.GetLines(currentLine, currentLine + 1);
                        if (lineText.Contains("<"))
                        {
                            foundStart = true;
                            break;
                        }

                        currentLine--;
                        if (currentLine > 1)
                            cursor.MoveToLineAndOffset(currentLine - 1, 1);  // 1 は行頭

                        else
                            cursor.MoveToLineAndOffset(1, 1);  // 0行目が存在しない場合、1行目に留める
                    }

                    if (!foundStart)
                        // 見つからなければ null（または例外・エラー通知）
                        return null;

                    // 開始タグ行から、">"が見つかるまで文字列を構築

                    while (!lineText.Contains(">"))
                    {
                        builder.AppendLine(lineText.Trim());
                        currentLine++;
                        cursor.MoveToLineAndOffset(currentLine - 1, 1);  // 1 は行頭
                        lineText = cursor.GetLines(currentLine, currentLine + 1);

                        // 無限ループ防止（ファイル末端まで達したら終了）
                        if (string.IsNullOrEmpty(lineText))
                            break;
                    }

                    // 最後の">"を含む行も追加
                    builder.AppendLine(lineText.Trim());
                    Debug.WriteLine(builder.ToString());
                }
                // XMLとしてパース
                var xmlText = builder.ToString().TrimEnd('\r', '\n',' ');
                if (!xmlText.Contains("xmlns:x=\""))
                {
                    if (xmlText.EndsWith("/>"))
                        xmlText = xmlText.Replace("/>", " " + x + " />");

                    else if (xmlText.EndsWith(">"))
                        xmlText = xmlText.Replace(">", " " + x + " />");

                }
                else if ( !xmlText.EndsWith("/>") && lineText.EndsWith(">"))
                    xmlText = xmlText.Replace(">", " />");

                return XElement.Parse(xmlText);
            }
            catch
            {
                return null;
            }
        }

        public static XElement ExtractElementFromBuffer(IWpfTextView textView, int cursorLine)
        {
            var snapshot = textView.TextSnapshot;
            var totalLines = snapshot.LineCount;

            int startLine = -1;
            int endLine = -1;

            // 上方向に <TextBox など開始タグの検出
            for (int i = cursorLine; i >= 0; i--)
            {
                var lineText = snapshot.GetLineFromLineNumber(i).GetText();
                if (lineText.Contains("<TextBox"))
                {
                    startLine = i;
                    break;
                }
            }

            // 下方向に /> または </TextBox> の検出
            for (int i = cursorLine; i < totalLines; i++)
            {
                var lineText = snapshot.GetLineFromLineNumber(i).GetText();
                if (lineText.Contains("/>") || lineText.Contains("</TextBox>"))
                {
                    endLine = i;
                    break;
                }
            }

            if (startLine == -1 || endLine == -1 || endLine < startLine)
                return null; // 抽出失敗（安全ガード）

            // 抽出したXAML断片を連結
            var builder = new StringBuilder();
            for (int i = startLine; i <= endLine; i++)
            {
                builder.AppendLine(snapshot.GetLineFromLineNumber(i).GetText());
            }

            string fragment = builder.ToString();

            try
            {
                var element = XElement.Parse(fragment);
                return element;
            }
            catch
            {
                return null; // XMLとして不整形なら null 返却
            }
        }
    }
}
