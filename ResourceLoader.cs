using System;
using System.Collections;
using System.Collections.Generic;
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
        private static string cachedProjectPath;
        private static Dictionary<string, Dictionary<string, string>> cachedResources;
        private static readonly Regex CultureRegex = new Regex(@"Resources(?:\.(?<culture>[a-z]{2}(?:-[A-Z]{2})?))?\.resx$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static void Save(string baseFolderPath, string ResourceFolder, string developType)
        {
            if (cachedResources.Count > 0)
            {

                var addedFolderPath = Path.Combine(baseFolderPath, ResourceFolder);
                var suffix = developType == "resw" ? "resw" : "resx";

                foreach (var langEntry in cachedResources)
                {
                    var langCode = langEntry.Key;
                    var kvps = langEntry.Value;

                    string langFolder = string.Empty;
                    if (developType == "resw")
                    {
                        langFolder = Path.Combine(addedFolderPath, langCode);
                        Directory.CreateDirectory(langFolder); // フォルダがない場合は作成
                    }
                    else
                        langFolder = addedFolderPath;

                    var reswFilePath = string.Empty;
                    if (developType == "resw")
                        reswFilePath = Path.Combine(langFolder, "Resources.resw");
                    else
                        reswFilePath = Path.Combine(langFolder, $"Resources.{langCode}.resx");

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

            string[] resCodes = new string[1];
            string suffix = string.Empty;
            if (developType == "resw")
            {
                resCodes = Directory.GetDirectories(baseFolderPath);
                suffix = "resw";
            }
            else
            {
                resCodes[0] = baseFolderPath;
                suffix = "resx";
            }
            string resFilename = $"Resources*.{suffix}";
            foreach (var reswFolder in resCodes)
            {
                // すべての .resx ファイル取得
                var resxFiles = Directory.GetFiles(reswFolder, resFilename, SearchOption.AllDirectories);

                foreach (var file in resxFiles)
                {
                    string langCode;
                    if (developType == "resw")
                        langCode = reswFolder.Substring(baseFolderPath.Length + 1); // "ja", "en" などを取得

                    else
                        langCode = file.Split('.')[file.Split('.').Length - 2];

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
