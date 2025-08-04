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
        public List<string> allCodes = new List<string>();

        private string baseFolderPath = string.Empty;

        private string lastKeyName = string.Empty;
        private Dictionary<string,Dictionary<string,string>> cacheList = new Dictionary<string,Dictionary<string,string>>();

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
        private readonly IServiceProvider _serviceProvider;

        public ResourceEditWindow(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            InitializeComponent();
            this.DataContext = this; // ← これが必要！
            LoadSettings();
        }

        private void LoadSettings()
        {
            var shellSettingsManager = new ShellSettingsManager(_serviceProvider); // ← ServiceProvider を渡すか取得する
            var store = shellSettingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

            if (!store.CollectionExists("ResourceMaker"))
                store.CreateCollection("ResourceMaker");

            if (!store.PropertyExists("ResourceMaker", "AccessMethod"))
                store.SetString("ResourceMaker", "AccessMethod", "loader.getString");

            AccessMethod = store.GetString("ResourceMaker", "AccessMethod");
            // UIに反映するなど
            ResourceGetterBox.Text = AccessMethod;
        }

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
            this.SelectedText.Text = this.LineText;
            this.ResultText.Text = this.LineText;
            string targetName = string.Empty;
            var matches = Regex.Matches(LineText, "\"([^\"]+)\"");
            foreach (Match match in matches)
            {
                targetName = match.Groups[1].Value;
                Debug.WriteLine(targetName); // → Strings

            }

            cacheList = ResourceCacheController.Get(baseFolderPath);
            ResourceKeyBox.Text = targetName;

            bool noValue = true;
            LanguageEntries.Clear();
            foreach (var code in allCodes.Distinct())
            {
                try
                {
                    var val = cacheList[code][targetName];
                    if(code == LangCulture)
                        LanguageEntries.Insert(0,new LanguageEntry(code, val));
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
            foreach ( var langCode in allCodes)
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
                bool isExist = false;
                var text = textBox.Text;
                string value;
                if (lastKeyName != text)
                {
                    foreach (var entry in cacheList)
                    {
                        var dic = entry.Value;
                        if (dic.TryGetValue(text, out  value))
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
                    if (EditorType == "code")
                    {
                        string targetName = string.Empty;
                        var matches = Regex.Matches(LineText, "\"([^\"]+)\"");
                        foreach (Match match in matches)
                        {
                            targetName = match.Groups[1].Value;
                            Debug.WriteLine(targetName); // → Strings
                        }
                        if (!string.IsNullOrEmpty(targetName) && Feedback.IsChecked == true)
                        {
                            string modifiedString = string.Empty;
                            modifiedString = ResourceGetterBox.Text + "(\"" + ResourceKeyBox.Text + "\")";
                            ResultText.Text = SelectedText.Text.Replace("\"" + targetName + "\"", modifiedString);
                            FeedbackText = ResultText.Text;
                        }
                    }
                }
            }
        }
    }
}
