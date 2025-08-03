using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;

namespace ResourceMaker
{
    public static class ResourceCacheLoader
    {
        private static string cachedProjectPath;
        private static Dictionary<string, Dictionary<string, string>> cachedResources;

        public static Dictionary<string, Dictionary<string, string>> Get(string currentProjectPath)
        {
            if (cachedProjectPath == currentProjectPath && cachedResources != null)
            {
                return cachedResources;
            }

            // 読み込み処理（例：.resxファイルのパースなど）
            cachedResources = LoadAllLanguageResources(currentProjectPath + @"\String");
            cachedProjectPath = currentProjectPath;

            return cachedResources;
        }

        public static Dictionary<string, Dictionary<string, string>> LoadAllLanguageResources(string baseFolderPath)
        {
            var result = new Dictionary<string, Dictionary<string, string>>();

            // すべての .resx ファイル取得
            var resxFiles = Directory.GetFiles(baseFolderPath, "Resources.resw", SearchOption.AllDirectories);

            foreach (var file in resxFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(file); // 例: Strings.ja
                string[] parts = fileName.Split('.');
                if (parts.Length < 2) continue;

                string langCode = parts[1]; // "ja", "en" などを取得

                var kvps = new Dictionary<string, string>();

                using (ResXResourceReader reader = new ResXResourceReader(file))
                {
                    foreach (DictionaryEntry entry in reader)
                    {
                        if (entry.Key is string key && entry.Value is string value)
                        {
                            kvps[key] = value;
                        }
                    }
                }

                result[langCode] = kvps;
            }

            return result;
        }
    }
}
