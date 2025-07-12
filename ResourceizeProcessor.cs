using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResourceMaker
{
    internal static class ResourceizeProcessor
    {
        public static void Run(DTE2 dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread(); // 追加
            var selection = dte?.ActiveDocument?.Selection as TextSelection;
            if (selection == null)
                return;

            string text = selection.Text;
            string message = string.IsNullOrWhiteSpace(text)
                ? "← 押されたよ！（選択なし）"
                : $"← 押されたよ！（選択: {text}）";

            selection.Insert(message);
        }
    }
}
