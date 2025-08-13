using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Xml.Linq;

namespace ResourceMaker
{
    public static class ResourceCacheController
    {
        private static string cachedProjectPath;
        private static Dictionary<string, Dictionary<string, string>> cachedResources;

        public static void Save( string baseFolderPath)
        {
            if (cachedResources.Count > 0)
            {
                var addedFolderPath = baseFolderPath + @"\Strings";

                foreach (var langEntry in cachedResources)
                {
                    var langCode = langEntry.Key;
                    var kvps = langEntry.Value;

                    var langFolder = Path.Combine(addedFolderPath, langCode);
                    Directory.CreateDirectory(langFolder); // フォルダがない場合は作成

                    var reswFilePath = Path.Combine(langFolder, "Resources.resw");

                    var doc = new XDocument(
                        new XElement("root",
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
                                new XElement("value", "System.Resources.ResXResourceWriter, System.Windows.Forms")),
                            kvps.Select(kvp =>
                                new XElement("data",
                                    new XAttribute("name", kvp.Key),
                                    new XAttribute(XNamespace.Xml + "space", "preserve"),
                                    new XElement("value", kvp.Value)))
                        )
                    );

                    doc.Save(reswFilePath);
                }
            }
        }
        public static void Clear()
        {
            cachedResources = null;
        }
        public static Dictionary<string, Dictionary<string, string>> Get(string currentProjectPath)
        {
            if (cachedProjectPath == currentProjectPath && cachedResources != null && cachedResources.Count != 0)
            {
                return cachedResources;
            }

            // 読み込み処理（例：.resxファイルのパースなど）
            cachedResources = LoadAllLanguageResources(currentProjectPath + @"\Strings");
            cachedProjectPath = currentProjectPath;

            return cachedResources;
        }

        public static Dictionary<string, Dictionary<string, string>> LoadAllLanguageResources(string baseFolderPath)
        {
            var result = new Dictionary<string, Dictionary<string, string>>();

            var reswFolders = Directory.GetDirectories(baseFolderPath);

            foreach (var reswFolder in reswFolders)
            {
                // すべての .resx ファイル取得
                var resxFiles = Directory.GetFiles(reswFolder, @"Resources.resw", SearchOption.AllDirectories);

                foreach (var file in resxFiles)
                {
                    string langCode = reswFolder.Substring(baseFolderPath.Length + 1); // "ja", "en" などを取得

                    var kvps = new Dictionary<string, string>();

                    try
                    {
                        var doc = XDocument.Load(file);
                        var dataElements = doc.Root?.Elements("data");

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
                    catch { }
                }
            }
            return result;
        }
    }
}
