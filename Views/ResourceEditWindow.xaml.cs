using CommunityToolkit.Mvvm.Input;
#if UI
#else
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.Settings;
#endif
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media.Animation;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace ResourceMaker.UI
{
    /// <summary>
    /// ResourceEditWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class ResourceEditWindow : Window
    {
        private static readonly Regex MyRegex = new Regex(@"^[a-z]{2}(-[A-Z]{2})?$", RegexOptions.Compiled);
        private static readonly Regex CultureRegex = new Regex(@"^(?<basename>.+?)\.(?<culture>[a-z]{2}(?:-[A-Z]{2})?)\.resx$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex candidateRegex = new Regex("\"([^\"]*)\"");

        public string LineText { get; set; } = string.Empty;
        public string EditorType { get; set; } = string.Empty;
        public string FeedbackText { get; set; } = string.Empty;
        public string LangCulture { get; set; } = string.Empty;

        public string DevelopType { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        
        private HashSet<string> resourceKeys = new HashSet<string>();

        private string resourcerFilename = string.Empty;
        public XElement element { get; set; }
        private string xUid = string.Empty;

        public List<string> allCodes = new List<string>();
        bool noValue = true;
        bool isToolTip = false;
        bool isEditOnly = false;

        private string baseFolderPath = string.Empty;

        private bool isUpdating = false;
        private bool isUidMode = false;

        private string resFolderName = string.Empty;
        private string resName = string.Empty;
        private string resSuffix = string.Empty;
        private bool isUWP = false;

        private int _localized = 0;

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
        public string AccessMethod = string.Empty;

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
            loader = new ResourceManager("ResourceMaker.Resources.Strings", GetType().Assembly);
            //リソース呼び出し
            this.LayoutUpdated += (_, __) =>
            {
                if (_localized == 0)
                {
                    LocalizeScreenXaml();
                    _localized++;
                    Debug.WriteLine($"Screen {_localized}");
                }
            };

            //LocalizeScreenXaml();
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
        private void LocalizeScreenXaml()
        {

            LabelTargetText.Text = loader.GetString("LabelTargetText.Text");
            LabelUpdatedText.Text = loader.GetString("LabelUpdatedText.Text");
            OriginalText.Text = loader.GetString("OriginalText.Text");
            ResourceKey.Text = loader.GetString("ResourceKey.Text");
            Feedback.Content = loader.GetString("Feedback.Content");
            SaveAndClose.Content = loader.GetString("SaveAndClose.Content");
            SaveResource.Content = loader.GetString("SaveResource.Content");
            AddLanguage.Content = loader.GetString("AddLanguage.Content");
            ClearCache.Content = loader.GetString("ClearCache.Content");
            BtnMakeLoaderCode.Content = loader.GetString("BtnMakeLoaderCode.Content");
            ManageLanguage.Content = loader.GetString("ManageLanguage.Content");
            ResourceGetterExpression.Content = loader.GetString("ResourceGetterExpression.Content");

            ToolTipService.SetToolTip(ResultText, loader.GetString("ResultTextToolTip"));
            ToolTipService.SetToolTip(BaseTexts, loader.GetString("BaseTextsToolTip"));
            ToolTipService.SetToolTip(ResourceKey, loader.GetString("ResourceKeyToolTip"));
            ToolTipService.SetToolTip(ResourceGetterBox, loader.GetString("ResourceGetterBoxToolTip"));
            ToolTipService.SetToolTip(Feedback, loader.GetString("FeedbackToolTip"));
            ToolTipService.SetToolTip(SaveResource, loader.GetString("SaveResourceToolTip"));
            ToolTipService.SetToolTip(SaveAndClose, loader.GetString("SaveAndCloseToolTip"));
            ToolTipService.SetToolTip(AddLanguage, loader.GetString("AddLanguageToolTip"));
            ToolTipService.SetToolTip(ClearCache, loader.GetString("ClearCacheToolTip"));
            ToolTipService.SetToolTip(ResourceGetterExpression, loader.GetString("ResourceGetterExpressionToolTip"));

        }
#endif
        public ResourceEditWindow()
        {
            InitializeComponent();
            this.DataContext = this; // ← これが必要！
#if UI
            AccessMethod = Properties.Settings.Default.AccessMethod;
#endif
            loader = new ResourceManager("ResourceMaker.Resources.Strings", GetType().Assembly);

        }

        //フォルダーがセットされると呼ばれます。
        private void LoadLanguageOptionsFromFolder(string basePath)
        {
            var tmp = DevelopType.Split('.');
            resFolderName = tmp[0];
            resName = tmp[1];
            resSuffix = tmp[2];
            isUWP = resSuffix == "resw";
            resourcerFilename = Path.Combine(basePath, "ResourceAdder.cs");

            ResourceGetterBox.Text = AccessMethod;

            ClearButton.Tag = "Testing";

            var resourcesRoot = Path.Combine(baseFolderPath, resFolderName);
            if (!Directory.Exists(resourcesRoot))
            {
                ResourceCacheController.Clear();
                DevelopType = "";
                var result = System.Windows.MessageBox.Show(
                    loader.GetString("MessageResourceFileNotFound"), loader.GetString("TitleResourceRetrievalFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation
                );
                return;
            }
            //設定済み言語リストの作成
            if (isUWP)
            { //UWP
                allCodes = Directory.GetDirectories(resourcesRoot)
                    .Select(Path.GetFileName)
                   .Where(name => MyRegex.IsMatch(name ?? string.Empty))
                   .Distinct()
                   .ToList();
            }
            else
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
                    .Where(culture => !string.IsNullOrEmpty(culture))  // 中立言語を除外するならここでフィルタ
                    .Distinct()
                    .ToList();

            }

            //置き換え対象の収集
            string targetName = string.Empty;
            var itemName = string.Empty;
            var matches = candidateRegex.Matches(LineText);

            foreach (Match match in matches)
            {
                itemName = match.Groups[1].Value;

                if (!string.IsNullOrEmpty(itemName)) // → Strings
                    BaseTexts.Items.Add(itemName);

            }
            //取得できていれば、先頭を表示
            if (BaseTexts.Items.Count > 0)
            {
                isUpdating = true;
                BaseTexts.SelectedIndex = 0;
                targetName = BaseTexts.Text;
                isUpdating = false;
            }
            cacheList = ResourceCacheController.Get(baseFolderPath, resFolderName, DevelopType);

            //キャッシュからキーの一覧を作成する

            if (allCodes.Count > 0 && cacheList.TryGetValue(allCodes[0], out var innerDict))
                resourceKeys = innerDict.Keys.ToHashSet();

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
            if (EditorType == "xaml" && !string.IsNullOrEmpty(LineText))
            {
                if (BaseTexts.Items.Count > 1 && LineText.Contains("ToolTipService.ToolTip"))
                {
                    MessageBox.Show(
                        loader.GetString("MessageTooltipConflict"),
                        loader.GetString("TitleTooltipConflict"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Exclamation
                    );
                    FeedbackText = "NG";
                    return;

                }
                if( element == null)
                {

                }
                MakeXamlForm();
                //なんか変
                //targetName = BaseTexts.Text;
                targetName = ResourceKeyBox.Text;
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
                    existingEntry.Value = BaseTexts.Text;
                }
            }
            Feedback.IsChecked = noValue;
            if (BaseTexts.Items.Count != 1)
            {
                SaveResource.Visibility = Visibility.Visible;
                SaveResource.IsEnabled = true;
            }
            else
            {
                SaveResource.Visibility = Visibility.Collapsed;
                SaveResource.IsEnabled = false;
            }

            if( BaseTexts.Items.Count == 0)
            {
                CodeTextArea.Visibility = Visibility.Collapsed;
                Getters.Visibility = Visibility.Collapsed;
                Feedback.IsChecked = false;
                this.Title = loader.GetString("TitleResourceEditingMode");
                ResourceKeyBox.Text = "";
                ResourceScroll.MaxHeight = 300;
                InformationPanel.Visibility = Visibility.Collapsed;
                AddLanguage.Visibility = Visibility.Collapsed;
                ManageLanguage.Visibility = Visibility.Visible;
                BtnMakeLoaderCode.Visibility = Visibility.Visible;
                isEditOnly = true;
            }
        }

        private string ReplaceXamlValue(string input, string target, string value)
        {
            string replaced;
            target += "=\"";
            int startIndex = input.IndexOf(target);
            if (startIndex >= 0)
            {
                int valueStart = startIndex + target.Length;
                int valueEnd = input.IndexOf("\"", valueStart);

                if (valueEnd > valueStart)
                {
                    string before = input.Substring(0, valueStart);
                    string after = input.Substring(valueEnd);
                    replaced = before + value + after;

                    Console.WriteLine(replaced);
                }
                else
                    return input;
            }
            else
                return input;

            return replaced;
        }

        //LoaderCodeの作成
        private void MakeLoaderText_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string keyName = ResourceKeyBox.Text;
                if (string.IsNullOrEmpty(keyName))
                {
                    var dialogRes = MessageBox.Show(
                        loader.GetString("MessageGenerateResourceCode"), loader.GetString("TitleGenerateResourceCode"),
                        MessageBoxButton.OKCancel,
                        MessageBoxImage.Exclamation
                    );
                    if (dialogRes == MessageBoxResult.OK)
                    {
                        File.Delete(resourcerFilename);
                        var keyList = cacheList["en-US"].Keys.OrderBy(k => k).ToArray();
                        foreach (var key in keyList)
                        {
                            File.AppendAllText(resourcerFilename, MakeLoaderText(key));
                        }
                    }

                }
                else
                    File.AppendAllText(resourcerFilename, MakeLoaderText(keyName));
            }
            catch (Exception ex)
            {
                var msg = string.Format( loader.GetString("MessageResourceAdderOutputError"), ex.Message);
                MessageBox.Show(
                    msg,
                    loader.GetString("TitleFileWriteError"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation
                );
            }
        }
        private string MakeLoaderText(string keyName)
        {
            string result;
            if( keyName.EndsWith("ToolTip"))
            {
                // ToolTipService.SetToolTip(ClearCache, loader.GetString("ClearCacheToolTip"));
                var key = keyName.Replace("ToolTip", "");
                result = $"ToolTipService.SetToolTip({key}, {ResourceGetterBox.Text}(\"{keyName}\"));{Environment.NewLine}";
            }
            else
                // BtnCsvOut.Content = loader.GetString("BtnCsvOut.Content");
                result = $"{keyName} = {ResourceGetterBox.Text}(\"{keyName}\");{Environment.NewLine}";
            return result;
        }

        //リソースの保存
        private void SaveResource_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                try
                {
                    string keyName = ResourceKeyBox.Text;
                    if (!string.IsNullOrEmpty(keyName))
                    {
                        if (!isUWP && !isToolTip && !isEditOnly && EditorType == "xaml")
                            File.AppendAllText(resourcerFilename, $"{keyName} = {ResourceGetterBox.Text}(\"{keyName}\");{Environment.NewLine}");

                        if (isToolTip)
                        {
                            var keyName2 = keyName.Replace(".", "");
                            if (ResultText.Text.Contains(".SetToolTip("))
                            {
                                var localKeyName = ResultText.Text.Replace(keyName, keyName2);
                                var pos = localKeyName.IndexOf("//");
                                if (pos > 0)
                                    File.AppendAllText(resourcerFilename, localKeyName.Substring(0, pos - 1) + Environment.NewLine);

                                ResultText.Text = ReplaceXamlValue(LineText, "ToolTipService.ToolTip", keyName2);
                                keyName = keyName2;
                            }
                            else
                            {
                                ResultText.Text = ReplaceXamlValue(ResultText.Text, "ToolTipService.ToolTip", keyName2);
                                var t = $"ToolTipService.SetToolTip({(keyName.Split(new[] { '.' }))[0]}, {ResourceGetterBox.Text}(\"{keyName2}\"));";
                                File.AppendAllText(resourcerFilename, t + Environment.NewLine);
                            }
                            keyName = keyName2;
                        }


                        //キャッシュに書き戻す
                        foreach (var langCode in allCodes)
                        {
                            var existingEntry = LanguageEntries
                                .FirstOrDefault(entry => entry.Code == langCode);

                            cacheList[langCode][keyName] = existingEntry.Value;
                            if (isEditOnly)
                                existingEntry.Value = "";
                        }

                        resourceKeys.Add(keyName);

                        if (button.Name == "SaveAndClose")
                        {
                            ResourceCacheController.Save(baseFolderPath, resFolderName, DevelopType);
                            FeedbackText = ResultText.Text;
                            this.Close();
                        }
                    }
                    if (button.Name == "SaveAndClose")
                        this.Close();

                    SaveResource.IsEnabled = false;
                    if (isEditOnly)
                    {
                        ResourceKeyBox.Text = "";
                        SuggestionList.SelectedItem = null;
                    }
                }
                catch (Exception ex)
                {
                    Infomation.Text = ex.Message;
                }
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
            var keyName = ResourceKeyBox.Text;

            if ( !string.IsNullOrEmpty(keyName))
            {
                if (isEditOnly)
                {
                    var innerDict = cacheList["en-US"];
                    if (innerDict.ContainsKey(keyName))
                    {
                        string msg = string.Format(loader.GetString("MessageConfirmKeyDeletionAllLanguages"), keyName);

                        var result = System.Windows.MessageBox.Show(
                            msg,
                            loader.GetString("TitleConfirmResourceDeletion"),
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question
                        );
                        if (result == MessageBoxResult.Yes)
                        {
                            // Yesが押されたときの処理
                            foreach (var langCode in allCodes)
                            {
                                if (cacheList.ContainsKey(langCode))
                                {
                                    innerDict = cacheList[langCode];
                                    innerDict.Remove(keyName);
                                    var existingEntry = LanguageEntries
                                        .FirstOrDefault(entry => entry.Code == langCode);
                                    existingEntry.Value = "";
                                }
                            }
                            resourceKeys.Remove(keyName);
                            //実ファイルも保存
                            ResourceCacheController.Save(baseFolderPath, resFolderName, DevelopType);
                        }
                    }
                    ResourceKeyBox.Text = "";


                }
                else
                    ResourceKeyBox.Text = "";
            }
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

        private void ResourceKeyBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (textBox.Tag is null && !SuggestionPopup.IsOpen)
                    textBox.Tag = textBox.Text;
            }
        }

        //リソースの名前を定義するボックス
        private void ResourceKeyBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (SuggestionPopup.IsOpen)
                    return;

                //if (textBox.Tag is string oldValue && textBox.Text != oldValue)
                ResouceKeyUpdate(textBox);// テキストが変わっていたらね

            }
        }
        private void ResouceKeyUpdate(TextBox textBox)
        {
            SaveResource.IsEnabled = true;
            Feedback.IsChecked = true;
            textBox.Tag = null;
            var text = textBox.Text;
            string value;
            var dic = cacheList["en-US"];
            if (dic.TryGetValue(text, out value))
            {
                //既に存在している値ならテキストを表示する
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

            if (isEditOnly)
                return;

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
                    if (BaseTexts.Items.Count == 1)
                        resText = LineText.Replace("\"" + targetName + "\"", modifiedString);
                    else
                        resText = ResultText.Text.Replace("\"" + targetName + "\"", modifiedString);

                    ResultText.Text = resText;
                }
                else if (EditorType == "xaml")
                {
                    var baseStr = ResultText.Text;
                    var tmp = ResourceKeyBox.Text.Split('.');
                    if (string.IsNullOrEmpty(xUid))
                        xUid = tmp[0];

                    string pattern = @"(x:" + (isUWP ? "Uid" : "Name") + @"\s*=\s*"")[^""]+("")";
                    string replacement = "$1" + tmp[0] + "$2";

                    isUpdating = true;
                    ResultText.Text = Regex.Replace(baseStr, pattern, replacement);
                    isUpdating = false;

                }
            }

        }
        private string GetUid(string name)
        {

            return element?.Attribute(x + name)?.Value ?? string.Empty;
        }

        private string GetXmlValue(string name)
        {
            return element?.Attribute(name)?.Value ?? string.Empty;
        }

        private void MakeXamlForm()
        {
            int adjPos = 4;
            string itemsKey = string.Empty;
            string result = string.Empty;
            string searchWord = string.Empty;
            bool needXUid = false;
            bool noName = true;
            if (isUWP)
                searchWord = "Uid";
            else
                searchWord = "Name";

            var pos = LineText.IndexOf("x:" + searchWord + "=");

            if (pos == -1 && !isUWP)
            {
                pos = LineText.IndexOf(" " + searchWord + "=");
                adjPos--;
            }
            if (pos > -1)
            {
                string keyname = string.Empty;
                pos += (searchWord.Length + adjPos);

                var pos2 = LineText.IndexOf('\"', pos);
                if (pos2 != -1)
                    keyname = LineText.Substring(pos, pos2 - pos);

                if (!string.IsNullOrEmpty(keyname))
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

                        if (filteredKeys.Count != 0)
                        {
                            isUpdating = true;
                            BaseTexts.Items.Clear(); // 必要に応じて初期化
                            isUidMode = true;

                            foreach (var key in filteredKeys)
                            {
                                BaseTexts.Items.Add(key);
                            }
                            isUpdating = false;
                            BaseTexts.SelectedIndex = 0;
                            Feedback.IsChecked = false;
                            ResourceKeyBox.IsReadOnly = true;
                            return;
                        }
                    }
                }
            }
            //x:Uidまたはx:Nameを検索
            string uidName = GetUid(searchWord);
            string xName = string.Empty;

            if (String.IsNullOrEmpty(uidName))
            {// x:Nameを検索
                if (isUWP)// UWPならx:Uidを補填しないとね。
                    needXUid = true;

                uidName = GetUid("Name");
                if (String.IsNullOrEmpty(uidName))
                {
                    uidName = GetXmlValue("Name");
                    noName = String.IsNullOrEmpty(uidName);
                }
                else
                    noName = false;
            }
            else
            {
                noName = false;

            }

            if (!noName)
            {
                var itemToRemove = BaseTexts.Items
                .Cast<object>()
                .FirstOrDefault(item => item?.ToString() == uidName);

                if (itemToRemove != null)
                {
                    isUpdating = true;
                    BaseTexts.Items.Remove(itemToRemove);
                    BaseTexts.SelectedIndex = 0;
                    isUpdating = false;
                }
            }
            itemsKey = GetXmlKeyname(BaseTexts.Text);
            if (itemsKey == "ToolTipService.ToolTip")
            {
                isToolTip = true;
                itemsKey = "ToolTip";
            }

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
                xUid = uidName;
                ResourceKeyBox.Text = $"{uidName}.{itemsKey}";
            }
            //結果置き場
            if ((!isUWP && string.IsNullOrEmpty(uidName) && string.IsNullOrEmpty(xName)) || needXUid)
            {// x:Uidを捏造挿入
                if (!noName && needXUid)
                    resultText = uidName;

                if (LineText.StartsWith("<"))
                    ResultText.Text = InsertUid(LineText, resultText, searchWord);

                else
                {
                    if (isToolTip)
                        ResultText.Text = $"x:{searchWord}=\"{resultText}\" {LineText.Replace(BaseTexts.Text, loader.GetString("NoteOutputToResourceAdder"))}";
                    else
                        ResultText.Text = $"x:{searchWord}=\"{resultText}\" {LineText.Replace(BaseTexts.Text, "")}";
                }
            }
            else
            {
                if (isToolTip)
                    ResultText.Text = $"ToolTipService.SetToolTip({uidName}, {ResourceGetterBox.Text}(\"{ResourceKeyBox.Text}\")); //" + 
                        loader.GetString("NoteOutputToResourceAdder");

                else
                    ResultText.Text = LineText;
                noValue = false;
            }

        }

        public string InsertUid(string xamlLine, string uidValue, string keyWord)
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
            string insert = $" x:{keyWord}=\"{uidValue}\"";

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

        private void ResultText_LostFocus(object sender, RoutedEventArgs e)
        {
            if (isUpdating || EditorType != "xaml")
                return;

            if (sender is TextBox textBox && textBox.Tag is string oldValue && textBox.Text != oldValue)
            {
                var tmp = ResourceKeyBox.Text.Split('.');
                var text = textBox.Text;

                string pattern = @"x:" + (isUWP ? "Uid" : "Name") + @"\s*=\s*""([^""]+)""";

                Match match = Regex.Match(text, pattern);

                if (match.Success)
                    tmp[0] = match.Groups[1].Value;  // ← これで "NoNameElement" のような値だけ取得できます

                isUpdating = true;
                ResourceKeyBox.Text = string.Join(".", tmp);
                isUpdating = false;


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
            langWindow.Language = Language;
            langWindow.isEditOnly = isEditOnly;
            langWindow.BaseFolderPath = BaseFolderPath;
            var result = langWindow.ShowDialog();
            FeedbackText = string.Empty;

            if (result == true)
                this.Close();

            //言語テキストをクリアする
            foreach (var entry in LanguageEntries)
            {
                entry.Value = string.Empty;
            }

            isUpdating = true;
            ResourceKeyBox.Text = "";
            isUpdating = false;

            ResourceCacheController.Clear();
        }

        //置き換え候補コンボ
        private void BaseTexts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdating) return;
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
                    keyName = $"{xUid}.{text}";

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
            string matchedProperty = element?.Attributes()
                .FirstOrDefault(attr => attr.Value == target)?.Name.LocalName;
            if (string.IsNullOrEmpty(matchedProperty))
                return string.Empty;
            return matchedProperty;
        }

        private void ResourceGetterExpression_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            // "ResourceGetterBox" の ToolTip を取得

            //loaderの設定
            string loaderGuide;
            if (isUWP)
                loaderGuide = $"Windows.ApplicationModel.Resources.ResourceLoader loader = ResourceLoader.GetForViewIndependentUse();";

            else
                loaderGuide = $"System.Resources.ResourceManager loader = new ResourceManager(\"{ProjectName}.{resFolderName}.{resName}\", GetType().Assembly);";

            var tooltip = loaderGuide;

            // クリップボードにコピー
            Clipboard.SetText(tooltip);
            ClipText.Text = tooltip;
            Infomation.Text = loader.GetString("LoaderDeclaration");
        }

        private void ClearCache_Click(object sender, RoutedEventArgs e)
        {
            ResourceCacheController.Clear();
            this.Close();
        }
        private void ResourceKeyBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isUpdating || !isEditOnly )
                return;

            string query = ResourceKeyBox.Text;
            if (query == "")
            {
                SuggestionPopup.IsOpen = false;
                return;
            }
            if (query == "*")
                query = "";
            else if (query[0] == '*')
            {
                query = query.Substring(1);
                ResourceKeyBox.Text = query;
                ResourceKeyBox.Select(query.Length, 0);

                return;
            }
            query = query.ToLower();
            var matches = resourceKeys
                .Where(k => k.ToLower().Contains(query)).OrderBy(k => k)
                .ToList();

            if (matches.Any())
            {
                if (matches.Count == 1 && matches[0].Equals(query, StringComparison.OrdinalIgnoreCase))
                {
                    SuggestionPopup.IsOpen = false;
                    return;
                }
                SuggestionList.ItemsSource = matches;
                SuggestionPopup.IsOpen = true;
            }
            else
                SuggestionPopup.IsOpen = false;
        }

        private void SuggestionList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && SuggestionList.SelectedItem is string selected)
                SelectTextFromList(selected);
            else if( e.Key == Key.Escape)
                SelectTextFromList(ResourceKeyBox.Text);

        }

        private void SelectTextFromList(string selected)
        {
            SuggestionPopup.IsOpen = false;
            SuggestionList.SelectedItem = null;
            ResourceKeyBox.Focus();
            ResourceKeyBox.Text = selected;
            ResourceKeyBox.CaretIndex = selected.Length;
            ResouceKeyUpdate(ResourceKeyBox);
        }
        private void SuggestionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Mouse.LeftButton == MouseButtonState.Pressed && SuggestionList.SelectedItem is string selected)
                SelectTextFromList(selected);

        }
        private void ResourceKeyBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {// エンターキー名なら、ポップアップの０番アイテムをテキストに送る
                if (SuggestionPopup.IsOpen)
                {
                    string selected = string.Empty;

                    if (SuggestionList.SelectedItem is string s)
                        selected = s;

                    else if (SuggestionList.Items.Count > 0 && SuggestionList.Items[0] is string first)
                        selected = first;

                    if (!string.IsNullOrEmpty(selected))
                    {
                        ResourceKeyBox.Text = selected;
                        SuggestionPopup.IsOpen = false;
                        ResourceKeyBox.CaretIndex = ResourceKeyBox.Text.Length;
                        e.Handled = true;
                    }
                }
                // 次のフォーカス可能な要素に移動
                e.Handled = true;
                TraversalRequest request = new TraversalRequest(FocusNavigationDirection.Next);
                UIElement focusedElement = Keyboard.FocusedElement as UIElement;
                if (focusedElement != null)
                    focusedElement.MoveFocus(request);
            }
            else if (e.Key == Key.Down)
            {//下矢印なら、ポップアップにフォーカスを移動する
                if (SuggestionPopup.IsOpen)
                {
                    e.Handled = true;
                    SuggestionList.SelectedIndex = 0;
                    var item = SuggestionList.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;
                    item.Focus(); // ListBoxItem に直接フォーカス

                }
            }
        }

        private void Button_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Button button)
            {
                button.Tag = button.Content;
                button.Content = loader.GetString("BtnClickme");
            }
        }

        private void Button_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Button button)
                button.Content = button.Tag;
        }

        private void LangBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox tb)
                tb.SelectAll();
        }

        private void LangBox_GotMouseButton(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox tb && !tb.IsKeyboardFocusWithin)
            {
                tb.Focus();
                e.Handled = true; // マウスクリックで選択解除されるのを防ぐ
            }
        }
    }
}
