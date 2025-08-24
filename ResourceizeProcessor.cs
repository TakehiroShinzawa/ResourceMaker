using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
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
using System.Xml;
using System.Xml.Linq;

namespace ResourceMaker
{
    internal static class ResourceizeProcessor
    {
        public static async Task RunAsync(DTE2 dte, IServiceProvider serviceProvider)
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
            ProjectItem item = activeDoc?.ProjectItem;
            Project owningProject = item?.ContainingProject;
            var projctFile = owningProject.FullName;
            string folder = Path.GetDirectoryName(projctFile);

            XElement element = null;
            //xamlをひっかける
            if (editorType == "xaml")
                element = ExtractTextBoxXml(start);

            int lcid = dte.LocaleID;
            CultureInfo culture = new CultureInfo(lcid);

            //リソースエディタの展開
            var resWindow = new ResourceEditWindow(serviceProvider);
            resWindow.LangCulture = culture.ToString();
            resWindow.LineText = lineText;
            resWindow.EditorType = editorType;
            resWindow.DevelopType = GetResourcePattern(projctFile, dte);
            resWindow.element = element;
            resWindow.BaseFolderPath = folder;
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
                    if (prop?.Name?.StartsWith("VsixProjectExtender.") == true)
                        return true;
                }
            }
            catch
            {
                // ログ出力などで補足可能
            }

            return false;
        }
        public static string GetResourcePattern(string csprojFile, DTE2 dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            XDocument doc = XDocument.Load(csprojFile);
            XNamespace ns = doc.Root.Name.Namespace;
            string resourceFolderTemplate = null;

            Project currentProject = dte?.ActiveDocument?.ProjectItem?.ContainingProject;
            bool isVsix = IsVsixProject(currentProject);
            if (IsVsixProject(currentProject))
                return "vsix";

            if (doc.Descendants(ns + "TargetPlatformIdentifier").Any(e => e.Value == "Windows"))
            {
                resourceFolderTemplate = @"Strings\{culture}\Resources.resw";
            }
            else if (doc.Descendants(ns + "PackageReference").Any(e => e.Attribute("Include")?.Value.Contains("Microsoft.WindowsAppSDK") == true))
            {
                resourceFolderTemplate = @"Strings\{culture}\Resources.resw";
            }
            else if (doc.Descendants(ns + "Reference").Any(e => e.Attribute("Include")?.Value.Contains("PresentationFramework") == true))
            {
                resourceFolderTemplate = @"Properties\Resources.{culture}.resx";
            }
            else if (doc.Descendants(ns + "UseWindowsForms").Any(e => e.Value == "true") ||
                     doc.Descendants(ns + "Reference").Any(e => e.Attribute("Include")?.Value.Contains("System.Windows.Forms") == true))
            {
                resourceFolderTemplate = @"Properties\Resources.{culture}.resx";
            }
            else if (doc.Root.Attribute("Sdk")?.Value.Contains("Microsoft.Maui.Sdk") == true)
            {
                resourceFolderTemplate = @"Resources.{culture}.resx";
            }
            else
                return "resx";
            return resourceFolderTemplate.Split('.')[1];
        }

        public static XElement ExtractTextBoxXml(EditPoint startPoint)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            StringBuilder builder = new StringBuilder();
            EditPoint cursor = startPoint.CreateEditPoint();
            int currentLine = cursor.Line;
            string lineText = string.Empty;
            bool foundStart = false;

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
                cursor.MoveToLineAndOffset(currentLine - 1, 1);  // 1 は行頭
            }

            if (!foundStart)
            {
                // 見つからなければ null（または例外・エラー通知）
                return null;
            }

            // 開始タグ行から、">"が見つかるまで文字列を構築
            bool doX = true;
            var x = "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";
            while (!lineText.Contains(">"))
            {
                builder.AppendLine(lineText.Trim());
                if (doX)
                {
                    builder.AppendLine(x);
                    doX = false;
                }
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

            // XMLとしてパース
            try
            {
                return XElement.Parse(builder.ToString());
            }
            catch (Exception ex)
            {
                // パース失敗時の処理（ログ or null）
                Debug.WriteLine(ex.Message);
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
