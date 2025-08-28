using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.SymbolStore;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Xml.Linq;

namespace ResourceMaker
{
    public static class ResourceCacheController
    {
        private static string cachedProjectPath = string.Empty;
        private static Dictionary<string, Dictionary<string, string>> cachedResources;
        private static List<XNode> cachedNodes;

        public static void Save(string baseFolderPath, string ResourceFolder, string developType)
        {
            if (cachedResources.Count > 0)
            {
                try
                {
                    var tmp = developType.Split('.');
                    var resFolderName = tmp[0];
                    var resName = tmp[1];
                    var resSuffix = tmp[2];

                    //ヘッダーの作成
                    if (cachedNodes == null || cachedNodes.Count == 0)
                    {
                        cachedNodes = new List<XNode>
                    {
                        new XElement("resheader",
                            new XAttribute("name", "resmimetype"),
                            new XElement("value", "text/microsoft-resx")),

                        new XElement("resheader",
                            new XAttribute("name", "version"),
                            new XElement("value", "2.0")),

                        new XElement("resheader",
                            new XAttribute("name", "reader"),
                            new XElement("value", "System.Resources.ResXResourceReader, System.Windows.Forms")),

                        new XElement("resheader",
                            new XAttribute("name", "writer"),
                            new XElement("value", "System.Resources.ResXResourceWriter, System.Windows.Forms"))
                    };
                    }

                    var addedFolderPath = Path.Combine(baseFolderPath, ResourceFolder);

                    foreach (var langEntry in cachedResources)
                    {
                        var langCode = langEntry.Key;
                        var kvps = langEntry.Value;

                        string langFolder = string.Empty;
                        if (resSuffix == "resw")
                        {
                            langFolder = Path.Combine(addedFolderPath, langCode);
                            Directory.CreateDirectory(langFolder); // フォルダがない場合は作成
                        }
                        else
                            langFolder = addedFolderPath;

                        var reswFilePath = string.Empty;
                        if (resSuffix == "resw")
                            reswFilePath = Path.Combine(langFolder, $"{resName}.{resSuffix}");
                        else
                            reswFilePath = Path.Combine(langFolder, $"{resName}.{langCode}.{resSuffix}");

                        var newDataElements = kvps.Select(kvp =>
                            new XElement("data",
                                new XAttribute("name", kvp.Key),
                                new XAttribute(XNamespace.Xml + "space", "preserve"),
                                new XElement("value", kvp.Value)
                            )
                        );

                        var doc = new XDocument(
                            new XElement("root",
                                cachedNodes,
                                newDataElements
                             )
                        );

                        doc.Save(reswFilePath);
                        if (langCode == "en-US")
                            File.Copy(reswFilePath, reswFilePath.Replace("en-US.", ""), overwrite: true);

                    }
                }
                catch { throw; }
            }
        }
        public static void Clear()
        {
            cachedResources = null;
            cachedProjectPath = string.Empty;
            cachedNodes = null;
        }
        public static Dictionary<string, Dictionary<string, string>> Get(string currentProjectPath, string ResourceFolder, string developType)
        {
            if (cachedProjectPath == currentProjectPath && cachedResources != null && cachedResources.Count != 0)
                return cachedResources;

            // 読み込み処理（例：.resxファイルのパースなど）
            cachedResources = LoadAllLanguageResources(Path.Combine(currentProjectPath, ResourceFolder), developType);
            cachedProjectPath = currentProjectPath;

            return cachedResources;
        }


        public static Dictionary<string, Dictionary<string, string>> LoadAllLanguageResources(string baseFolderPath, string developType)
        {
            var result = new Dictionary<string, Dictionary<string, string>>();

            bool isFirst = true;

            var tmp = developType.Split('.');
            var resFolderName = tmp[0];
            var resName = tmp[1];
            var resSuffix = tmp[2];

            string[] resCodes = new string[1];
            if (resSuffix == "resw")
                resCodes = Directory.GetDirectories(baseFolderPath);

            else
                resCodes[0] = baseFolderPath;

            string resFilename = $"{resName}*.{resSuffix}";
            string skipName = $"{resName}.{resSuffix}";
            foreach (var reswFolder in resCodes)
            {
                // すべての .resx ファイル取得
                var resxFiles = Directory.GetFiles(reswFolder, resFilename, SearchOption.AllDirectories);

                foreach (var file in resxFiles)
                {
                    string langCode;
                    if (resSuffix == "resw")
                        langCode = reswFolder.Substring(baseFolderPath.Length + 1); // "ja", "en" などを取得

                    else
                    {
                        var parts = file.Split('.');
                        var num = parts.Length;
                        if (file.EndsWith(skipName, StringComparison.OrdinalIgnoreCase))
                            continue;

                        langCode = file.Split('.')[file.Split('.').Length - 2];
                    }
                    var kvps = new Dictionary<string, string>();

                    //try
                    {
                        var doc = XDocument.Load(file);
                        var dataElements = doc.Root?.Elements("data");
                        if (isFirst)
                        {
                            var allNodes = doc.Root?.Nodes();
                            if (allNodes == null)
                            {
                                cachedNodes = new List<XNode>();
                                continue;
                            }

                            // <data> 要素以外を抽出
                            var filteredNodes = new List<XNode>();
                            foreach (var node in allNodes)
                            {
                                if (node is XElement e && e.Name == "data")
                                    continue;

                                if (node is XElement xe)
                                    filteredNodes.Add(new XElement(xe));
                                else if (node is XComment c)
                                    filteredNodes.Add(new XComment(c.Value));
                                else if (node is XText t)
                                    filteredNodes.Add(new XText(t.Value));
                                else if (node is XProcessingInstruction pi)
                                    filteredNodes.Add(new XProcessingInstruction(pi.Target, pi.Data));
                                else if (node is XDocumentType dt)
                                    filteredNodes.Add(new XDocumentType(dt.Name, dt.PublicId, dt.SystemId, dt.InternalSubset));
                                // 他の型は無視（必要ならログ出力や例外処理も可）
                            }

                            cachedNodes = filteredNodes;
                            isFirst = false;
                        }
                        if (dataElements != null)
                        {
                            foreach (var data in dataElements)
                            {
                                var key = data.Attribute("name").Value;
                                var value = data.Element("value").Value;

                                if (!string.IsNullOrEmpty(key) && value != null)
                                    kvps[key] = value;
                            }
                            //if(kvps.Count > 0 )
                            result[langCode] = kvps;
                        }

                    }
                    //catch { }
                }
            }
            return result;
        }
    }
}
