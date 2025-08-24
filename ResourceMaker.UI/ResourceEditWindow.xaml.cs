using CommunityToolkit.Mvvm.Input;
#if UI
#else
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell.Settings;
#endif
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Linq;

namespace ResourceMaker.UI
{
    /// <summary>
    /// ResourceEditWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class ResourceEditWindow : Window
    {
        private static readonly Regex MyRegex = new Regex(@"^[a-z]{2}(-[A-Z]{2})?$", RegexOptions.Compiled);
        private static readonly Regex CultureRegex = new Regex(@"Resources(?:\.(?<culture>[a-z]{2}(?:-[A-Z]{2})?))?\.resx$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public string LineText { get; set; } = string.Empty;
        public string EditorType { get; set; } = string.Empty;
        public string FeedbackText { get; set; } = string.Empty;
        public string LangCulture { get; set; } = string.Empty;

        public string DevelopType { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;

        public XElement element { get; set; }
        private string xUid = string.Empty;

        public List<string> allCodes = new List<string>();
        bool noValue = true;
        private string baseFolderPath = string.Empty;

        private bool isUpdating = false;
        private bool isUidMode = false;
        private string rootFolderName = string.Empty;
        private string loaderGuide = string.Empty;


        private string lastKeyName = string.Empty;
        private Dictionary<string, Dictionary<string, string>> cacheList = new Dictionary<string, Dictionary<string, string>>();

        private XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        private System.Resources.ResourceManager loader;
            

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string BaseFolderPath
        {
            get => baseFolderPath;
            set
            {
                if (baseFolderPath != value)
                {
                    baseFolderPath = value;
                    OnPropertyChanged(nameof(BaseFolderPath));
                    LoadLanguageOptionsFromFolder(baseFolderPath); // ステップ①呼び出し！
                }
            }
        }

        public ObservableCollection<LanguageEntry> LanguageEntries { get; } = new ObservableCollection<LanguageEntry>();
        private string AccessMethod = string.Empty;

#if UI
        private void SaveSettings()
        {
            AccessMethod = ResourceGetterBox.Text;
            Properties.Settings.Default.AccessMethod = AccessMethod;
            Properties.Settings.Default.Save();

        }
#else
        private readonly System.IServiceProvider _serviceProvider;

        public ResourceEditWindow(System.IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            InitializeComponent();
            this.DataContext = this; // ← これが必要！
            LoadSettings();
            this.Closing += (s, e) =>
            {
                // クローズ前に行いたい処理をここに記述
                if (Feedback.IsChecked == false)
                    FeedbackText = string.Empty;
            };
        }

        private void LoadSettings()
        {

            var shellSettingsManager = new ShellSettingsManager(_serviceProvider); // ← ServiceProvider を渡すか取得する
            var store = shellSettingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

            if (!store.CollectionExists("ResourceMaker"))
                store.CreateCollection("ResourceMaker");

            store.DeleteProperty("ResourceMaker", "AccessMethod");


            if (!store.PropertyExists("ResourceMaker", "AccessMethod"))
                store.SetString("ResourceMaker", "AccessMethod", "loader.GetString");

            AccessMethod = store.GetString("ResourceMaker", "AccessMethod");
            // UIに反映するなど
            ResourceGetterBox.Text = AccessMethod;
        }

        private void SaveSettings()
        {
            var shellSettingsManager = new ShellSettingsManager(_serviceProvider);
            var store = shellSettingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

            if (!store.CollectionExists("ResourceMaker"))
                store.CreateCollection("ResourceMaker");

            AccessMethod = ResourceGetterBox.Text;
            // UIの値を取得して保存
            store.SetString("ResourceMaker", "AccessMethod", AccessMethod);
        }

#endif
        public ResourceEditWindow()
        {
            InitializeComponent();
            this.DataContext = this; // ← これが必要！
#if UI
            AccessMethod = Properties.Settings.Default.AccessMethod;
#endif
            ResourceGetterBox.Text = AccessMethod;

        }

        //フォルダーがセットされると呼ばれます。
        private void LoadLanguageOptionsFromFolder(string basePath)
        {
            loader = new ResourceManager("ResourceMaker.LanguageResources.Resources", GetType().Assembly);
            switch (DevelopType)
            {
                case "resw":
                    rootFolderName = "Strings";
                    break;
                case "resx":
                    rootFolderName = "Properties";
                    break;
                case "vsix":
                    rootFolderName = "LanguageResources";
                    break;
            }
            var ressourcePath = $"{ProjectName}.{rootFolderName}.Resources";
            loaderGuide = $"System.Resources.ResourceManager loader = \r\n    new ResourceManager(\"{ProjectName}.{rootFolderName}.Resources\", typeof(ResourceEditWindow).Assembly);";
            //        private readonly System.Resources.ResourceManager loader = 
            //new ResourceManager("ResourceMaker.LanguageResources.Resources", typeof(ResourceEditWindow).Assembly);


            var resourcesRoot = Path.Combine(BaseFolderPath, rootFolderName);

            //リソースの状態検査
            bool isClose = false;
            var stringsRoot = Path.Combine(BaseFolderPath, rootFolderName);
            if (!Directory.Exists(stringsRoot))
            {
                var langWindow = new LanguageSelectionWindow();
                langWindow.DevelopType = DevelopType;
                langWindow.BaseFolderPath = BaseFolderPath;
                if (langWindow.ShowDialog() == false)
                    isClose = true;

                else
                    allCodes = langWindow.allCodes;
            }
            else
            {

                if (DevelopType == "resw")
                { //UWP
                    allCodes = Directory.GetDirectories(resourcesRoot)
                        .Select(Path.GetFileName)
                       .Where(name => MyRegex.IsMatch(name ?? string.Empty))
                       .Distinct()
                       .ToList();
                }
                else if (DevelopType.Substring(1) == "resx" || DevelopType == "vsix")
                {// WPF MAUI vsix
                    allCodes = Directory.GetFiles(resourcesRoot)
                        .Select(path => Path.GetFileName(path))
                        .Select(file =>
                        {
                            var match = CultureRegex.Match(file ?? string.Empty);
                            return match.Success
                                ? match.Groups["culture"].Value // 空文字列なら中立言語
                                : null;
                        })
                        .Where(culture => culture != null) // 中立言語を除外するならここでフィルタ
                        .Distinct()
                        .ToList();
                }

                if (allCodes.Count == 0)
                {
                    var langWindow = new LanguageSelectionWindow();
                    langWindow.DevelopType = DevelopType;
                    langWindow.BaseFolderPath = BaseFolderPath;
                    if (langWindow.ShowDialog() == false)
                        isClose = true;
                    else
                        allCodes = langWindow.allCodes;
                }

            }
            if (isClose)
                this.Close();

            //置き換え対象の収集
            string targetName = string.Empty;
            var itemName = string.Empty;
            var matches = Regex.Matches(LineText, "\"([^\"]+)\"");

            foreach (Match match in matches)
            {
                itemName = match.Groups[1].Value;

                Debug.WriteLine(itemName); // → Strings
                BaseTexts.Items.Add(itemName);

            }
            //取得できていれば、先頭を表示
            if (!string.IsNullOrEmpty(itemName))
            {
                isUpdating = true;
                BaseTexts.SelectedIndex = 0;
                targetName = BaseTexts.Text;
                isUpdating = false;
            }
            cacheList = ResourceCacheController.Get(baseFolderPath,rootFolderName, DevelopType);

            //言語セットを作成する
            LanguageEntries.Clear();
            foreach (var code in allCodes)
            {
                if (code == LangCulture)
                    LanguageEntries.Insert(0, new LanguageEntry(code, ""));
                else
                    LanguageEntries.Add(new LanguageEntry(code, ""));

            }

            //選択行の文字列種別で初期化
            SelectedText.Text = LineText;
            if (EditorType == "xaml")
            {
                MakeXamlForm();
                targetName = BaseTexts.Text;
                if (isUidMode) return;
            }
            else
            {
                ResultText.Text = LineText;
                ResourceKeyBox.Text = targetName;
            }
            noValue = true;
            LangCulture = CultureInfo.CurrentUICulture.Name;

            foreach (var code in allCodes)
            {
                try
                {
                    var val = cacheList[code][targetName];

                    var existingEntry = LanguageEntries
                        .FirstOrDefault(entry => entry.Code == code);
                    existingEntry.Value = val;
                    noValue = false;
                }
                catch
                {
                    var existingEntry = LanguageEntries
                        .FirstOrDefault(entry => entry.Code == code);
                    existingEntry.Value = targetName;
                }
            }
            Feedback.IsChecked = noValue;
            if (BaseTexts.Items.Count > 1)
                SaveResource.Visibility = Visibility.Visible;
            else
                SaveResource.Visibility = Visibility.Collapsed;
            
            SaveResource.IsEnabled = (BaseTexts.Items.Count > 1);
        }

        //リソースの保存
        private void SaveResource_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                //キャッシュに書き戻す
                foreach (var langCode in allCodes)
                {
                    var existingEntry = LanguageEntries
                        .FirstOrDefault(entry => entry.Code == langCode);

                    cacheList[langCode][ResourceKeyBox.Text] = existingEntry.Value;
                }
                ResourceCacheController.Save(baseFolderPath, rootFolderName,DevelopType);
                if (button.Name == "SaveAndClose")
                {
                    FeedbackText = ResultText.Text;
                    this.Close();
                }
                SaveResource.IsEnabled = false;
            }
        }

        public class LanguageEntry : INotifyPropertyChanged
        {
            public string Code { get; }

            private string _value;
            public string Value
            {
                get => _value;
                set
                {
                    if (_value != value)
                    {
                        _value = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
                    }
                }
            }

            public LanguageEntry(string code)
            {
                Code = code;
                _value = string.Empty;
            }
            public LanguageEntry(string code, string value)
            {
                Code = code;
                _value = value;
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        private void ResourceClearButton_Click(object sender, RoutedEventArgs e)
        {
            ResourceKeyBox.Clear();
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;

                // 次のフォーカス可能な要素に移動
                TraversalRequest request = new TraversalRequest(FocusNavigationDirection.Next);
                UIElement focusedElement = Keyboard.FocusedElement as UIElement;
                if (focusedElement != null)
                    focusedElement.MoveFocus(request);
            }
        }
        public ICommand ClearCommand => new RelayCommand<LanguageEntry>(entry =>
        {
            if (entry != null) entry.Value = string.Empty;
        });

        //リソースの名前を定義するボックス
        private void ResourceKeyBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (textBox.Tag is string oldValue && textBox.Text != oldValue)
                {// テキストが変わっていたらね
                    SaveResource.IsEnabled = true;
                    Feedback.IsChecked = true;
                    textBox.Tag = null;
                    bool isExist = false;
                    var text = textBox.Text;
                    string value;
                    foreach (var entry in cacheList)
                    {
                        var dic = entry.Value;
                        if (dic.TryGetValue(text, out value))
                        {
                            isExist = true;
                            break;
                        }
                    }
                    if (isExist)
                    {//既に存在している値ならテキストを表示する
                        foreach (var langCode in allCodes)
                        {
                            if (cacheList.TryGetValue(langCode, out var dict) &&
                                dict.TryGetValue(text, out var localizedValue))
                            {
                                var existingEntry = LanguageEntries
                                    .FirstOrDefault(entry => entry.Code == langCode);

                                if (existingEntry != null)
                                    existingEntry.Value = localizedValue;

                            }
                        }
                    }
                    //変換テキストの作成
                    string targetName = BaseTexts.Text;

                    if (!string.IsNullOrEmpty(targetName) && Feedback.IsChecked == true)
                    {
                        string modifiedString = string.Empty;
                        if (EditorType == "code")
                        {
                            if (noValue)
                                modifiedString = ResourceGetterBox.Text + "(\"" + ResourceKeyBox.Text + "\")";
                            else
                                modifiedString = "\"" + ResourceKeyBox.Text + "\"";

                            var resText = string.Empty;
                            if ( BaseTexts.Items.Count == 1)
                                resText = LineText.Replace("\"" + targetName + "\"", modifiedString);
                            else
                                resText = ResultText.Text.Replace("\"" + targetName + "\"", modifiedString);

                            ResultText.Text = resText;
                        }
                        else if (EditorType == "xaml")
                        {
                            var tmp = ResourceKeyBox.Text.Split('.');
                            var resText = $"x:Uid=\"{tmp[0]}\"";
                            ResultText.Text = ResultText.Text.Replace("x:Uid=\"NoNameElement\"", resText);
                        }
                    }
                }
            }
        }

        private string GetUid(string name)
        {
            return element.Attribute(x + name)?.Value ?? string.Empty;
        }

        private string GetXmlValue(string name)
        {
            return element.Attribute(name)?.Value ?? string.Empty;
        }

        private void MakeXamlForm()
        {
            string itemsKey = string.Empty;
            string result = string.Empty;
            bool noName = true;
            var pos = LineText.IndexOf("x:Uid=");
            if (pos > -1)
            {
                string keyname = string.Empty;
                pos += 7;
                var pos2 = LineText.IndexOf('\"', pos);
                if (pos2 != -1)
                    keyname = LineText.Substring(pos, pos2 - pos);
                
                if(!string.IsNullOrEmpty(keyname))
                // uidなら設定済みデータを登録
                {
                    Dictionary<string, string> firstInnerDict = cacheList.Values.FirstOrDefault();
                    string prefix = keyname + ".";
                    if (firstInnerDict != null)
                    {
                        xUid = keyname;
                        var filteredKeys = firstInnerDict.Keys
                            .Where(k => k.StartsWith(prefix))
                            .Select(k => k.Substring(prefix.Length))
                            .ToList();

                        isUpdating = true;
                        BaseTexts.Items.Clear(); // 必要に応じて初期化
                        isUidMode = true;

                        foreach (var key in filteredKeys)
                        {
                            BaseTexts.Items.Add(key);
                        }
                        isUpdating = false;
                        BaseTexts.SelectedIndex = 0;
                        return;
                    }
                }
            }
            //x:Uidを検索
            string uidName = GetUid("Uid");
            string xName = string.Empty;

            if (String.IsNullOrEmpty(uidName))
            {// x:Nameを検索
                xName = GetUid("Name");
                if (String.IsNullOrEmpty(xName))
                {
                    xName = GetXmlValue("Name");
                    noName = String.IsNullOrEmpty(xName);
                }
                else
                    noName = false;
            }
            else
                noName = false;

            //

            itemsKey = GetXmlKeyname(BaseTexts.Text);
            string resultText = string.Empty;

            if (noName)
            {//名無しなので、x:Uidの入力を促す
                resultText = "NoNameElement";
                ResourceKeyBox.Text = "NoNameElement." + itemsKey;

                ResourceKeyBox.Focus();
                ResourceKeyBox.Select(0, 13);
            }
            else
            {
                if (string.IsNullOrEmpty(uidName))
                {// ネームからxUidを生成
                    resultText = xName;
                    ResourceKeyBox.Text = $"{xName}.{itemsKey}";
                    xUid = xName;
                }
                else
                {//uid発見なら
                    xUid = uidName;
                    ResourceKeyBox.Text = $"{uidName}.{itemsKey}";
                }
            }
            //結果置き場
            if (string.IsNullOrEmpty(uidName))
            {// x:Uidを捏造挿入
                if (LineText.StartsWith("<"))
                    ResultText.Text = InsertUid(LineText, resultText);

                else
                    ResultText.Text = $" x:Uid=\"{resultText}\" {LineText.Replace(BaseTexts.Text,"")}";
            }
            else
            {
                ResultText.Text = LineText;
            }
        }

        public string InsertUid(string xamlLine, string uidValue)
        {
            // タグ名の終わり（空白 or '>'）を探す
            int tagEndIndex = -1;
            for (int i = 1; i < xamlLine.Length; i++)
            {
                char c = xamlLine[i];
                if (char.IsWhiteSpace(c) || c == '>')
                {
                    tagEndIndex = i;
                    break;
                }
            }
            if (tagEndIndex < 0) return xamlLine;

            // 挿入する文字列
            string insert = $" x:Uid=\"{uidValue}\"";

            // タグ名直後に挿入
            return xamlLine.Insert(tagEndIndex, insert);
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (textBox.Tag is null)
                    textBox.Tag = textBox.Text;
            }
        }

        private void ResourceGetterBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (textBox.Tag is string oldValue && textBox.Text != oldValue)
                {
                    SaveSettings();
                    textBox.Tag = null;
                }
            }
        }

        //言語の追加削除ボタン
        private void AddResource(object sender, RoutedEventArgs e)
        {
            var langWindow = new LanguageSelectionWindow();
            langWindow.DevelopType = DevelopType;
            langWindow.EditorType = EditorType;
            langWindow.LineText = LineText;
            langWindow.BaseFolderPath = BaseFolderPath;
            var result = langWindow.ShowDialog();
            FeedbackText = string.Empty;
            ResourceCacheController.Clear();
            this.Close();
        }

        //置き換え候補コンボ
        private void BaseTexts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(isUpdating) return;
            string text = BaseTexts.SelectedItem.ToString();
            if (EditorType == "code")
            {
                ResourceKeyBox.Text = text;
                foreach (var langCode in allCodes)
                {
                    if (cacheList.TryGetValue(langCode, out var dict) &&
                        dict.TryGetValue(text, out var localizedValue))
                    {
                        var existingEntry = LanguageEntries
                            .FirstOrDefault(entry => entry.Code == langCode);

                        if (existingEntry != null)
                            existingEntry.Value = localizedValue;

                    }
                }
            }
            else
            {
                string keyName = string.Empty;
                if (isUidMode)
                {
                    keyName = $"{xUid}.{text}";
                }
                else
                {
                    string matchedProperty = GetXmlKeyname(text);
                    keyName = $"{xUid}.{matchedProperty}";
                }
                ResourceKeyBox.Text = keyName;
                foreach (var langCode in allCodes)
                {
                    var existingEntry = LanguageEntries
                        .FirstOrDefault(entry => entry.Code == langCode);
                    if (cacheList.TryGetValue(langCode, out var dict) &&
                        dict.TryGetValue(keyName, out var localizedValue))
                    {

                        if (existingEntry != null)
                            existingEntry.Value = localizedValue;

                    }
                    else
                        existingEntry.Value = text;
                }
                SaveResource.IsEnabled = true;
            }
        }
        //値のキー名を取得する
        private string GetXmlKeyname(string target)
        {
            string matchedProperty = element.Attributes()
                .FirstOrDefault(attr => attr.Value == target)?.Name.LocalName;
            return matchedProperty;
        }
        
    }
}
