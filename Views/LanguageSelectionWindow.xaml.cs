using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Xml;

namespace ResourceMaker.UI
{
    /// <summary>
    /// LanguageSelectionWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class LanguageSelectionWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string LineText { get; set; } = string.Empty;
        public string EditorType { get; set; } = string.Empty;
        public string DevelopType { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string LangCulture { get; set; } = string.Empty;

        private string baseFolderPath = string.Empty;
        private string resFolderName = string.Empty;
        
        List<string> countries = new List<string>();
        
        private System.Resources.ResourceManager loader;
        private string resName = string.Empty;
        private string resSuffix = string.Empty;

        public bool isAddResource = false;

        public List<string> folderCodes = new List<string>();
        public List<string> allCodes = new List<string>();

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

        private static readonly Regex MyRegex = new Regex(@"^[a-z]{2}(-[A-Z]{2})?$", RegexOptions.Compiled);
        private static readonly Regex CultureRegex = new Regex(@"^(?<basename>.+?)\.(?<culture>[a-z]{2}(?:-[A-Z]{2})?)\.resx$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public ObservableCollection<LanguageOption> LanguageOptions { get; set; } = new ObservableCollection<LanguageOption>();
        public ObservableCollection<LanguageEntry> CustomLanguages { get; } = new ObservableCollection<LanguageEntry>();

        public LanguageSelectionWindow()
        {
            InitializeComponent();
            this.DataContext = this; // ← これが必要！
            loader = new ResourceManager("ResourceMaker.Resources.Strings", GetType().Assembly);
            LocalizeScreenXaml();

        }
        private void LocalizeScreenXaml()
        {
            SelectLanguagesGuide.Text = loader.GetString("SelectLanguagesGuide.Text");
            AdditionalLanguageCode.Text = loader.GetString("AdditionalLanguageCode.Text");
            ProjectSelector.Text = loader.GetString("ProjectSelector.Text");
            BtnCreate.Content = loader.GetString("BtnCreate.Content");
            BtnCancel.Content = loader.GetString("BtnCancel.Content");
            BtnClose.Content = loader.GetString("BtnClose.Content");
            BtnAdd.Content = loader.GetString("BtnAdd.Content");
        }
        public void LoadLanguageOptionsFromFolder(string basePath)
        {
            countries = new List<string>();

            if (string.IsNullOrEmpty(DevelopType))
            {
                ProjectTypePanel.Visibility = Visibility.Visible;
                LanguagePanel.Visibility = Visibility.Collapsed; 
            }
            else
            {
                var tmp = DevelopType.Split('.');
                resFolderName = tmp[0];
                resName = tmp[1];
                resSuffix = tmp[2];
            }

            var resourcesRoot = Path.Combine(BaseFolderPath, resFolderName);
            if (Directory.Exists(resourcesRoot))
            {
                // フォルダが存在している
                if (resSuffix == "resw")
                { //UWP
                    countries = Directory.GetDirectories(resourcesRoot)
                        .Select(Path.GetFileName)
                       .Where(name => MyRegex.IsMatch(name ?? string.Empty))
                       .Distinct()
                       .ToList();
                }
                else 
                {// WPF MAUI vsix
                    countries = Directory.GetFiles(resourcesRoot, $"{resName}.*.{resSuffix}")
                        .Select(path => Path.GetFileName(path))
                        .Select(file =>
                        {
                            var match = CultureRegex.Match(file ?? string.Empty);
                            return match.Success
                                ? match.Groups["culture"].Value // 空文字列なら中立言語
                                : null;
                        })
                        .Where(culture => !string.IsNullOrEmpty(culture)) // 中立言語を除外するならここでフィルタ
                        .Distinct()
                        .ToList();
                }
            }
            else
                // フォルダが存在していない
                Directory.CreateDirectory(resourcesRoot);

            LanguageOptions.Clear();
            // ① 初期言語を設定
            var defaultCodes = new[] { "ja-JP", "en-US", "zh-CN", "fr-FR" };
            foreach (var code in defaultCodes)
            {
                LanguageOptions.Add(new LanguageOption
                {
                    DisplayName = code,
                    CultureCode = code,
                    IsSelected = (code == "en-US" || code == LangCulture),
                    IsEnabled = (code != "en-US") // en-US はチェック不可
                });

            }

            // ② countries の内容で反映
            foreach (var folderCode in countries)
            {
                // 既存の言語オプションを検索（CultureCode一致）
                var existing = LanguageOptions.FirstOrDefault(o => o.CultureCode == folderCode);

                if (existing != null)   // 既存なら選択済みに更新
                    existing.IsSelected = true;

                else // なければ新規追加（選択済みで）
                    if (!string.IsNullOrWhiteSpace(folderCode))
                    LanguageOptions.Add(new LanguageOption
                    {
                        DisplayName = folderCode,
                        CultureCode = folderCode,
                        IsSelected = true,
                        IsEnabled = true
                    });
            }
        }

        private void ConfirmLanguage_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        public string NewLanguageInput { get; set; } = string.Empty;

        public ICommand AddNewLanguageCommand => new RelayCommand(() =>
        {
            // 入力があれば追加
            if (!string.IsNullOrWhiteSpace(NewLanguageInput) &&
                !CustomLanguages.Any(entry => entry.Code == NewLanguageInput))
            {
                CustomLanguages.Add(new LanguageEntry { Code = NewLanguageInput });
            }

            //// 入力欄を1つ追加（空白）
            //CustomLanguages.Add(new LanguageEntry { Code = string.Empty });

            // 入力欄クリア
            NewLanguageInput = string.Empty;
            OnPropertyChanged(nameof(NewLanguageInput));
        });

        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is LanguageEntry item)
            {
                var vm = this.DataContext as LanguageSelectionWindow;
                vm?.CustomLanguages.Remove(item);
            }
        }

        private void MakeFoldersCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        public async Task<bool> CreateLanguageCodeFoldersAsync(string basePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(basePath))
                    return false;

                var tmp = DevelopType.Split('.');
                resFolderName = tmp[0];
                resName = tmp[1];
                resSuffix = tmp[2];
                if (!Directory.Exists(basePath))
                    Directory.CreateDirectory(basePath);

                allCodes = LanguageOptions.Where(o => o.IsSelected)
                    .Select(o => o.CultureCode)
                    .Concat(CustomLanguages.Select(c => c.Code))
                    .Where(code => !string.IsNullOrWhiteSpace(code))
                    .Distinct()
                    .ToList();
                await Task.Run(() =>
                {
                    // 重たい処理（例：フォルダ作成など）
                    string folderBase = Path.Combine(basePath, resFolderName);
                    string folderPath = folderBase;
                    string fileName = $"{resName}.{resSuffix}";
                    if (!Directory.Exists(folderPath))
                        Directory.CreateDirectory(folderPath);

                    foreach (var code in allCodes)
                    {
                        if (resSuffix == "resw")
                        {
                            folderPath = Path.Combine(folderBase, code);
                            if (!Directory.Exists(folderPath))
                                Directory.CreateDirectory(folderPath);
                        }
                        else
                            fileName = $"{resName}.{code}.{resSuffix}";
                        //テンプレートの作成
                        string filePath = Path.Combine(folderPath, fileName);

                        if (!File.Exists(filePath))
                        {

                            XmlWriterSettings settings = new XmlWriterSettings
                            {
                                Indent = true,
                                Encoding = System.Text.Encoding.UTF8
                            };

                            using (XmlWriter writer = XmlWriter.Create(filePath, settings))
                            {
                                writer.WriteStartDocument(); // <?xml version="1.0" encoding="utf-8"?>
                                writer.WriteStartElement("root"); // <root>
                                writer.WriteEndElement();         // </root>
                                writer.WriteEndDocument();        // end of document
                            }
                        }
                    }
                    //削除も対応
                    List<string> notInAllCodes = new List<string>();
                    foreach (var item in countries)
                    {
                        if (!allCodes.Contains(item))
                            notInAllCodes.Add(item);

                    }
                    foreach (var code in notInAllCodes)
                    {
                        try
                        {
                            if (resSuffix == "resw")
                            {
                                folderPath = Path.Combine(basePath, code);
                                Directory.Delete(folderPath, true); // true で中身も再帰的に削除
                            }
                            else
                            {
                                fileName = $"{resFolderName}\\{resName}.{code}.{resSuffix}";
                                string fullPath = Path.Combine(basePath, fileName);
                                File.Delete(fullPath);
                            }
                        }
                        catch { }

                    }
                    ResourceCacheController.Clear();
                });
                return true;
            }
            catch
            {
                return false;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "VSTHRD100:Avoid async void", Justification = "WPF event handler")]
        private async void MakeFolders_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(DevelopType))
                    return;

                this.DialogResult = await CreateLanguageCodeFoldersAsync(BaseFolderPath);
                this.Close();
            }
            catch { }
        }

        private void ProjectType_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string type)
            {
                DevelopType = type;
                LanguagePanel.Visibility = Visibility.Visible;
                LoadLanguageOptionsFromFolder(baseFolderPath);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = !string.IsNullOrEmpty(DevelopType);
            this.Close();
        }
    }
    public class LanguageEntry : INotifyPropertyChanged
    {
        private string _code = string.Empty;
        public string Code
        {
            get => _code;
            set
            {
                _code = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Code)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
    public class LanguageOption
    {
        public string DisplayName { get; internal set; } = string.Empty;
        public string CultureCode { get; internal set; } = string.Empty;
        public bool IsSelected { get; set; } = false;
        public bool IsEnabled {  get; set; } = false;
    }
}
