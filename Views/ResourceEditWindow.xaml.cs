using CommunityToolkit.Mvvm.Input;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml;
using System.Xml.Linq;

namespace ResourceMaker.UI
{
    /// <summary>
    /// ResourceEditWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class ResourceEditWindow : Window
    {
        private static readonly Regex MyRegex = new Regex(@"^[a-z]{2}(-[A-Z]{2})?$", RegexOptions.Compiled);
        public string LineText { get; set; } = string.Empty;
        public string EditorType { get; set; } = string.Empty;
        public string FeedbackText { get; set; } = string.Empty;
        public string LangCulture { get; set; } = string.Empty;
        public XElement element { get; set; }

        public List<string> allCodes = new List<string>();
        bool noValue = true;
        private string baseFolderPath = string.Empty;

        private string lastKeyName = string.Empty;
        private Dictionary<string, Dictionary<string, string>> cacheList = new Dictionary<string, Dictionary<string, string>>();

        private XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

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
                if (Feedback.IsChecked == true)
                    FeedbackText = ResultText.Text;
                else
                    FeedbackText = string.Empty;
            };
        }

        private void LoadSettings()
        {
            var shellSettingsManager = new ShellSettingsManager(_serviceProvider); // ← ServiceProvider を渡すか取得する
            var store = shellSettingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

            if (!store.CollectionExists("ResourceMaker"))
                store.CreateCollection("ResourceMaker");

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

        private void LoadLanguageOptionsFromFolder(string basePath)
        {
            bool isClose = false;
            var stringsRoot = Path.Combine(BaseFolderPath, "Strings");
            if (!Directory.Exists(stringsRoot))
            {
                var langWindow = new LanguageSelectionWindow();
                langWindow.BaseFolderPath = BaseFolderPath;
                if (langWindow.ShowDialog() == false)
                    isClose = true;

                else
                    allCodes = langWindow.allCodes;
            }
            else
            {
                allCodes = Directory.GetDirectories(stringsRoot)
                    .Select(path => Path.GetFileName(path) ?? string.Empty)
                    .Where(name => MyRegex.IsMatch(name ?? string.Empty))
                    .ToList();
                if (allCodes.Count == 0)
                {
                    var langWindow = new LanguageSelectionWindow();
                    langWindow.BaseFolderPath = BaseFolderPath;
                    if (langWindow.ShowDialog() == false)
                        isClose = true;
                    else
                        allCodes = langWindow.allCodes;
                }

            }
            if (isClose)
                this.Close();
            //

            string targetName = string.Empty;
            var matches = Regex.Matches(LineText, "\"([^\"]+)\"");
            foreach (Match match in matches)
            {
                targetName = match.Groups[1].Value;
                Debug.WriteLine(targetName); // → Strings

            }

            cacheList = ResourceCacheController.Get(baseFolderPath);

            this.SelectedText.Text = this.LineText;
            if (EditorType == "xaml")
            {
                MakeXamlForm();
                targetName = ResourceKeyBox.Text;
            }
            else
            {
                this.ResultText.Text = this.LineText;
                ResourceKeyBox.Text = targetName;
            }
            noValue = true;

            LanguageEntries.Clear();
            foreach (var code in allCodes.Distinct())
            {
                try
                {
                    var val = cacheList[code][targetName];
                    if (code == LangCulture)
                        LanguageEntries.Insert(0, new LanguageEntry(code, val));
                    else
                        LanguageEntries.Add(new LanguageEntry(code, val));
                    noValue = false;
                }
                catch
                {
                    if (code == LangCulture)
                        LanguageEntries.Insert(0, new LanguageEntry(code, targetName));
                    else
                        LanguageEntries.Add(new LanguageEntry(code, targetName));
                }
            }
            Feedback.IsChecked = noValue;
        }
        private void SaveResource_Click(object sender, RoutedEventArgs e)
        {
            //キャッシュに書き戻す
            foreach (var langCode in allCodes)
            {
                var existingEntry = LanguageEntries
                    .FirstOrDefault(entry => entry.Code == langCode);

                cacheList[langCode][ResourceKeyBox.Text] = existingEntry.Value;
            }
            ResourceCacheController.Save(baseFolderPath);

            this.Close();
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
            ClearButton.Visibility = Visibility.Collapsed;
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

        private void ResourceKeyBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (textBox.Tag is string oldValue && textBox.Text != oldValue)
                {
                    Feedback.IsChecked = true;
                    textBox.Tag = null;
                    bool isExist = false;
                    var text = textBox.Text;
                    string value;
                    if (lastKeyName != text)
                    {
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
                        {
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
                        string targetName = string.Empty;
                        var matches = Regex.Matches(LineText, "\"([^\"]+)\"");
                        foreach (Match match in matches)
                        {
                            targetName = match.Groups[1].Value;
                        }

                        if (!string.IsNullOrEmpty(targetName) && Feedback.IsChecked == true)
                        {
                            string modifiedString = string.Empty;
                            if (EditorType == "code")
                            {
                                if (noValue)
                                    modifiedString = ResourceGetterBox.Text + "(\"" + ResourceKeyBox.Text + "\")";
                                else
                                    modifiedString = "\"" + ResourceKeyBox.Text + "\"";

                                FeedbackText = LineText.Replace("\"" + targetName + "\"", modifiedString);
                                ResultText.Text = FeedbackText;
                            }
                            else if (EditorType == "xaml")
                            {
                                if (ResultText.Text == "x:Uid=\"NoNameElement\"")
                                {
                                    var tmp = ResourceKeyBox.Text.Split('.');
                                    FeedbackText = $"x:Uid=\"{tmp[0]}\"";
                                    ResultText.Text = FeedbackText;
                                }
                            }
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
            var tmp = LineText.Split('=')[0].Split('.');

            itemsKey = tmp[tmp.Length - 1];
            if (noName)
            {//名無しなので、x:Uidの入力を促す
                ResultText.Text = "x:Uid=\"NoNameElement\"";
                ResourceKeyBox.Text = "NoNameElement." + itemsKey;

                ResourceKeyBox.Focus();
                ResourceKeyBox.Select(0, 13);
            }
            else
            {
                if (string.IsNullOrEmpty(uidName))
                {
                    ResultText.Text = $"x:Uid=\"{xName}\"";
                    ResourceKeyBox.Text = $"{xName}.{itemsKey}";
                }
                else
                {
                    ResultText.Text = " ";
                    ResourceKeyBox.Text = $"{uidName}.{itemsKey}";
                }
            }
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
    }
}
