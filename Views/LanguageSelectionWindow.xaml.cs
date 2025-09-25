using CommunityToolkit.Mvvm.Input;
using EnvDTE80;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Net.Configuration;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml;
using static System.Net.Mime.MediaTypeNames;

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

        private int csvOutMode = 0;
        private string csvOutFolder = string.Empty;

        public bool isEditOnly = false;
        public bool isAddResource = false;

        public List<string> folderCodes = new List<string>();
        public List<string> allCodes = new List<string>();
        private bool isUWP;

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
        private static readonly Regex FolderNameRegex = new Regex(@"^[^<>:""/\\|?*\x00-\x1F]+(?<![ .])$", RegexOptions.Compiled);

        private const string SegmentPattern = @"[^<>:""/\\|?*\x00-\x1F]+(?<![ .])";

        // パス全体を検証する正規表現（複数階層対応）
        private static readonly Regex FolderPathRegex = new Regex($"^({SegmentPattern})([\\\\/]({SegmentPattern}))*$", RegexOptions.Compiled);



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
            ApplyButton.Content = loader.GetString("ApplyButton.Content");
            BtnCsvOut.Content = loader.GetString("BtnCsvOut.Content");
            ChkCustom.Content = loader.GetString("ChkCustom.Content");
            CustomResFolder.Text = loader.GetString("CustomResFolder.Text");
            CustmResourceName.Text = loader.GetString("CustmResourceName.Text");
            CustomResPtn.Text = loader.GetString("CustomResPtn.Text");
            ChkResw.Content = loader.GetString("ChkResw.Content");
            ChkResx.Content = loader.GetString("ChkResx.Content");
            IsNewResource.Text = loader.GetString("IsNewResource.Text");
            BtnSelectAllLanguage.Content = loader.GetString("BtnSelectAllLanguage.Content");
            CsvOutputItem.Text = loader.GetString("CsvOutputItem.Text");
            optVOnly.Content = loader.GetString("optVOnly.Content");
            optKOnly.Content = loader.GetString("optKOnly.Content");
            optKVP.Content = loader.GetString("optKVP.Content");
            CsvOutputPath.Text = loader.GetString("CsvOutputPath.Text");
            RBSameFolder.Content = loader.GetString("RBSameFolder.Content");
            RBPrjTop.Content = loader.GetString("RBPrjTop.Content");
            RBClipboard.Content = loader.GetString("RBClipboard.Content");
            ChkWithHeader.Content = loader.GetString("ChkWithHeader.Content");
            GetCsv.Content = loader.GetString("GetCsv.Content");
            ImportFromClipboard.Content = loader.GetString("ImportFromClipboard.Content");
            LblCultureCode.Text = loader.GetString("LblCultureCode.Text");
            RelocateGroup.Header = loader.GetString("RelocateGroup.Header");
            LblCurrentResource.Text = loader.GetString("LblCurrentResource.Text");
            LblResourceFolder.Text = loader.GetString("LblResourceFolder.Text");
            LblResourceName.Text = loader.GetString("LblResourceName.Text");
            LblFileFormat.Text = loader.GetString("LblFileFormat.Text");
            LblRelocatedResource.Text = loader.GetString("LblRelocatedResource.Text");
            LblRelResourceFolder.Text = loader.GetString("LblRelResourceFolder.Text");
            LblRelResourceName.Text = loader.GetString("LblRelResourceName.Text");
            FileFormatResx.Content = loader.GetString("FileFormatResx.Content");
            FileFormatResw.Content = loader.GetString("FileFormatResw.Content");
            LblRelocationProjectFolder.Text = loader.GetString("LblRelocationProjectFolder.Text");
            BtnBrowseFolder.Content = loader.GetString("BtnBrowseFolder.Content");
            BtnRelocateResource.Content = loader.GetString("BtnRelocateResource.Content");
            LblRelFileFormat.Text = loader.GetString("LblRelFileFormat.Text");
            LanguageManageWindow.Title = loader.GetString("LanguageSelectionWindowName");
            HelpLinkText.Text = loader.GetString("HelpLinkText.Text");

            ToolTipService.SetToolTip(BtnCreate, loader.GetString("BtnCreateToolTip"));
            ToolTipService.SetToolTip(BtnCancel, loader.GetString("BtnCancelToolTip"));
            ToolTipService.SetToolTip(BtnClose, loader.GetString("BtnCloseToolTip"));
            ToolTipService.SetToolTip(BtnAdd, loader.GetString("BtnAddToolTip"));
            ToolTipService.SetToolTip(ChkWithHeader, loader.GetString("ChkWithHeaderToolTip"));
            ToolTipService.SetToolTip(ClipLangCode, loader.GetString("ClipLangCodeToolTip"));
            ToolTipService.SetToolTip(HelpLinkLang, loader.GetString("HelpLinkLangToolTip"));

        }

        RadioButton FindRadioButtonByTag(DependencyObject parent, string targetTag)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is RadioButton rb && rb.GroupName == "ProjectType")
                {
                    if (rb.Tag?.ToString() == targetTag)
                        return rb;
                }

                //var result = FindRadioButtonByTag(child, targetTag);
                //if (result != null)
                //    return result;
            }
            return null;
        }

        public void LoadLanguageOptionsFromFolder(string basePath, bool isFirst = false)
        {
            countries = new List<string>();

            if (string.IsNullOrEmpty(DevelopType))
            {
                ProjectTypePanel.Visibility = Visibility.Visible;
                LanguagePanel.Visibility = Visibility.Collapsed;
                FileControl.Visibility = Visibility.Collapsed;
                return;
            }
            var tmp = DevelopType.Split('.');
            resFolderName = tmp[0];
            resName = tmp[1];
            resSuffix = tmp[2];
            ProjectTypePanel.IsEnabled = isFirst;
            isUWP = resSuffix == "resw";
            var rb = FindRadioButtonByTag(ProjectOptions, DevelopType);
            if (rb == null)
            {
                ChkCustom.IsChecked = true;
                ResourceFolderBox.Text = resFolderName;
                ResourceFileNameBox.Text = resName;

                if (isUWP)
                    ChkResw.IsChecked = true;
                else
                    ChkResx.IsChecked = true;
            }
            else
                rb.IsChecked = true;

            var resourcesRoot = Path.Combine(basePath, resFolderName);

            //CSV出力先
            RBSameFolder.Tag = resourcesRoot;
            RBPrjTop.Tag = basePath;

            if (Directory.Exists(resourcesRoot))
            {
                // フォルダが存在している
                if (isUWP)
                { //UWP
                    countries = Directory.GetDirectories(resourcesRoot)
                        .Select(Path.GetFileName)
                       .Where(name => MyRegex.IsMatch(name ?? string.Empty))
                       .Distinct()
                       .ToList();
                }
                else if ((resSuffix == "resx"))
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
            if (!isEditOnly)
            {
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
                        IsSelected = !isEditOnly,
                        IsEnabled = true
                    });
            }
            //ファイルの検証を行う
            if (countries.Count == 0)
            {
                BtnClose.Visibility = Visibility.Collapsed;
                IsNewResource.Visibility = Visibility.Visible;
                ProjectTypePanel.IsEnabled = true;
            }
            else
            {
                BtnClose.Visibility = Visibility.Visible;
                IsNewResource.Visibility = Visibility.Collapsed;
            }

            //リソース編集モード
            if (isEditOnly && !isFirst)
            {
                IsNewResource.Visibility = Visibility.Collapsed;
                ProjectTypePanel.Visibility = Visibility.Collapsed;
                LanguageAdder.Visibility = Visibility.Collapsed;
                LanguageCommands.Visibility = Visibility.Collapsed;
                BtnSelectAllLanguage.Visibility = Visibility.Visible;
                //値設定開始
                LanguageManageWindow.Title = loader.GetString("LblResourceWindow");
                //再配置
                CurResourceFolder.Text = "./" + resFolderName;
                CurResourceName.Text = resName;
                string fmt;
                if (isUWP)
                    fmt = loader.GetString("ResSeparatedByFolder");

                else
                    fmt = loader.GetString("ResIntoFilename");

                CurResourceIdt.Text = string.Format(fmt, resSuffix);
            }
            else
                FileControl.Visibility = Visibility.Collapsed;
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
                        if (isUWP)
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
                            if (isUWP)
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

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void ProjectType_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string type)
            {
                CustomInfo.IsEnabled = false;
                var resourceParam = type.ToString().Replace('\\', '.').Replace('/','.').Split(new char[]  { '.'});
                ResourceFolderBox.Text = resourceParam[0];
                ResourceFileNameBox.Text = resourceParam[1];
                if (resourceParam[2].EndsWith("w"))
                    ChkResw.IsChecked = true;
                else
                    ChkResx.IsChecked = true;

                DevelopType = type;

                ApplyButton.IsEnabled = true;
            }
        }

        private void CustomType_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string type)
            {
                CustomInfo.IsEnabled = true;
                bottonControl();
            }
        }
        private bool IsCustomStatusOK()
        {
            bool isOK = true;
            
            //フォルダ名チェック
            string input = ResourceFolderBox.Text;
            if (!string.IsNullOrEmpty(input) && !FolderPathRegex.IsMatch(input))
                isOK = false;

            //ファイル名チェック
            input = ResourceFileNameBox.Text;
            if (!FolderNameRegex.IsMatch(input))
                isOK = false;

            return isOK;
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && !string.IsNullOrEmpty( DevelopType))
            {
                LanguagePanel.Visibility = Visibility.Visible;
                LoadLanguageOptionsFromFolder(baseFolderPath,true);
            }
        }

        private void bottonControl()
        {
            bool isOK = IsCustomStatusOK();
            ApplyButton.IsEnabled = isOK;
            if (isOK)
                DevelopType = ResourceFolderBox.Text.Replace("/", "\\").Replace(".", "\\") + "." +
                              ResourceFileNameBox.Text + "." + ((bool)ChkResw.IsChecked ? "resw" : "resx");
            else
                DevelopType = "";
        }
        private void ResourceLeave(object sender, RoutedEventArgs e)
        {
            bottonControl();
        }

        private void ResouceChk(object sender, RoutedEventArgs e)
        {
            bottonControl();
        }

        private void KeyDownHandeler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                (Keyboard.FocusedElement as UIElement)?.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }
        }

        private void OnBrowseFolderClick(object sender, RoutedEventArgs e)
        {
            var folder = NewFolderTextBox.Text;
            if (string.IsNullOrEmpty(folder))
                folder = baseFolderPath;

            var picker = new FolderPickerWindow
            {
                DataContext = new FolderPickerViewModel(folder),
                Owner = System.Windows.Application.Current.MainWindow
            };
            if (picker.ShowDialog() == true)
                NewFolderTextBox.Text = picker.SelectedFolderPath;

        }
        private void CsvRadioButton2_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox)
            {
                if (!(checkBox.IsChecked == true))
                {
                    optVOnly.IsEnabled = true;
                    optKOnly.IsEnabled = true;
                    return;
                }
                else
                {
                    optKVP.IsChecked = true;
                    optVOnly.IsEnabled = false;
                    optKOnly.IsEnabled = false;
                }
            }

        }

        private void CsvRadioButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radioButton)
            {
#if !IGNORE_TRY
                try
#endif
                {
                    if (radioButton.GroupName == "CsvTarget")
                    {
                        csvOutFolder = radioButton.Tag.ToString();
                        if (!(ChkWithHeader.IsChecked == true))
                        {
                            optVOnly.IsEnabled = true;
                            optKOnly.IsEnabled = true;
                        }
                    }
                    else
                        csvOutMode = int.Parse(radioButton.Tag.ToString());
                    
                }
#if !IGNORE_TRY
                catch
                {
                    Debug.WriteLine($"Check {radioButton.Content} Settings");
                }
#endif
            }
        }

        private bool CsvOutput(List<string> countries)
        {
            bool isNormal = true;
            string target;
            List<string> outputPath = new List<string>();

            //出力先の定義
            if (ChkWithHeader.IsChecked == true || (csvOutFolder == "Clipboard" && countries.Count > 1))
            {
                outputPath.Add($"{csvOutFolder}\\{resName}.csv");
                isNormal = false;
            }
            else
            {
                foreach (var country in countries)
                {
                    if (isUWP)
                    {
                        if(RBPrjTop.IsChecked == true)
                            target = $"{csvOutFolder}\\{resName}.csv";

                        else
                            target = $"{csvOutFolder}\\{country}\\{resName}.csv";
                    }
                    else
                        target = $"{csvOutFolder}\\{resName}.{country}.csv";
                    outputPath.Add(target);
                }
            }

            try
            {
                //リソース取得
                var allResource = ResourceCacheController.Get();
                if (allResource == null)
                    allResource = ResourceCacheController.Get(baseFolderPath, resFolderName, DevelopType);

                
                if (isNormal)
                {//通常のcsv 1:1出力
                    int i = 0;
                    foreach (var country in countries)
                    {
                        var innerDic = allResource[country];
                        var csvData = ReadResourceToCsv(innerDic);
                        if (csvOutFolder != "Clipboard")
                            File.WriteAllText(outputPath[i++], csvData, new UTF8Encoding(false));
    
                        else
                            Clipboard.SetText(csvData);

                    }
                    return false;
                }
                else
                {
                    var csvData = ReadMultiLangResourceToCsv(allResource, countries);
                    if(csvOutFolder != "Clipboard")
                        File.WriteAllText(outputPath[0], csvData, new UTF8Encoding(false));

                    else
                        Clipboard.SetText(csvData);
                }       
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message.ToString());
                return true;
            }
        }
        private string ReadMultiLangResourceToCsv(Dictionary<string, Dictionary<string, string>> cache, List<string> languages)
        {
            var sb = new StringBuilder();

            // ヘッダー行
            sb.Append("Key");
            foreach (var lang in languages)
                sb.Append($",{lang}");
            sb.AppendLine();

            // 基準言語（en-US）からキー一覧を取得
            if (!cache.TryGetValue("en-US", out var baseDict))
                throw new InvalidOperationException(loader.GetString("MissingResource_enUS"));

            foreach (var key in baseDict.Keys)
            {
                sb.Append(key);
                foreach (var lang in languages)
                {
                    string value = cache.TryGetValue(lang, out var langDict) && langDict.TryGetValue(key, out var v)
                        ? v : "";
                    sb.Append($",\"{EscapeCsv(value)}\"");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        // CSVエスケープ処理（カンマやダブルクォート対策）
        private string EscapeCsv(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return input.Contains(",") || input.Contains("\"") || input.Contains("\n")
                ? "\"" + input.Replace("\"", "\"\"") + "\""
                : input;
        }
        private string ReadResourceToCsv(Dictionary< string,string> Resource)
        {
            StringBuilder sb = new StringBuilder();

            foreach (var kvp in Resource)
            {
                switch (csvOutMode)
                {
                    case 1:
                        sb.AppendLine(kvp.Key);
                        break;
                    case 2:
                        sb.AppendLine(kvp.Value);
                        break;
                    case 3:
                        sb.AppendLine($"{kvp.Key},{kvp.Value}");
                        break;
                }
            }
            return sb.ToString();

        }
        private void BtnCsvOut_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && csvOutMode != 0 && !string.IsNullOrEmpty(csvOutFolder))
            {
                List<string> outList = new List<string>();

                foreach (var lo in LanguageOptions)
                {
                    if (lo.IsSelected)
                        outList.Add(lo.DisplayName);

                }
                if (outList.Count == 0)
                {
                    MessageBox.Show(
                        loader.GetString(loader.GetString("NoLanguageSelection")), loader.GetString("NoLanguageSelected"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Exclamation
                    );
                    return;
                }

                CsvOutput(outList);
                MessageBox.Show(
                    loader.GetString("CsvExportSuccess"), loader.GetString("CsvExportCompleted"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            else
            {
                MessageBox.Show(
                    loader.GetString("SelectCsvExportSettings"),
                    loader.GetString("MissingCsvExportSettings"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation
                );
                return;
            }
        }

        private void BtnCsvGet(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                Dictionary<string, string> inputList = new Dictionary<string, string>();
                if (btn.Tag is string s && s == "Clip")
                {//クリップボードの言語取得
                    var clipLang = ClipLangCode.Text;
                    if (Clipboard.ContainsText())
                    {

                        if (string.IsNullOrEmpty(clipLang))
                        {
                            foreach (var lo in LanguageOptions)
                            {
                                if (lo.IsSelected)
                                    inputList.Add(lo.DisplayName, "");

                            }
                            if (inputList.Count != 1)
                            {
                                MessageBox.Show(
                                    loader.GetString("SelectSingleLanguageForImport"),
                                    loader.GetString("LanguageSelectionRequired"),
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Exclamation
                                );
                                return;
                            }
                        }
                        else
                        {
                            if (IsCultureCode(clipLang))
                                inputList.Add(clipLang, "");
                            else
                            {
                                MessageBox.Show(
                                    loader.GetString("InvalidCultureCodeEntered"),
                                    loader.GetString("InvalidCultureCode"),
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Exclamation
                                );
                                return;
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show(
                            loader.GetString("NoTextInClipboard"),
                            loader.GetString("ClipboardEmpty"),
                            MessageBoxButton.OK,
                            MessageBoxImage.Exclamation
                        );
                        return;
                    }
                }
                else
                {
                    var dialog = new Microsoft.Win32.OpenFileDialog
                    {
                        Title = loader.GetString("SelectCsvFile"),
                        Filter = loader.GetString("CsvFileFilter"),
                        InitialDirectory = baseFolderPath, // ← ここがデフォルトフォルダ
                        Multiselect = true
                    };

                    bool? result = dialog.ShowDialog();

                    if (result == true)
                    {
                        string[] selectedFiles = dialog.FileNames;


                        foreach (var file in selectedFiles)
                        {
                            string fileName = Path.GetFileName(file);
                            var match = Regex.Match(fileName, @"(?<![a-zA-Z0-9])([a-z]{2}-[A-Z]{2})(?=\.)", RegexOptions.Compiled);

                            if (match.Success)
                            {
                                string cultureCode = match.Groups[1].Value;
                                inputList[cultureCode] = file; // cultureCode をキー、file（フルパス）を値
                            }
                        }
                        if (inputList.Count == 0)
                        {
                            MessageBox.Show(
                                loader.GetString("NoCultureCodeDetectedInFile"),
                                loader.GetString("CultureCodeNotFound"),
                                MessageBoxButton.OK,
                                MessageBoxImage.Exclamation
                            );
                            return;
                        }
                    }
                    else
                        return;

                }
                //リソース取得
                var allResource = ResourceCacheController.Get();
                if (allResource == null)
                    allResource = ResourceCacheController.Get(baseFolderPath, resFolderName, DevelopType);

                string resultCultures = string.Empty;

                foreach (var tar in inputList)
                {
                    var tarKey = tar.Key;
                    var resDic = LoadCsvToDictionary(tar.Value, allResource["en-US"]);
                    if (resDic == null)
                        continue;

                    resultCultures += $",{tarKey}";

                    if (!allResource.ContainsKey(tarKey))
                        allResource[tarKey] = new Dictionary<string, string>();

                    foreach (var kvp in resDic)
                    {
                        allResource[tarKey][kvp.Key] = kvp.Value; // 既存キーは上書き、新規キーは追加
                    }
                }
                if (!string.IsNullOrEmpty(resultCultures))
                {
                    ResourceCacheController.Save(baseFolderPath, resFolderName, DevelopType);

                    string title = string.Format(loader.GetString("LanguageResourceAddedToTarget"), resultCultures.Substring(1));
                    MessageBox.Show(
                        title,
                        loader.GetString("LanguageResourceAdded"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Exclamation
                    );
                }

            }
        }

        private bool IsCultureCode(string target)
        {
            return Regex.IsMatch(target, @"^[a-z]{2}-[A-Z]{2}$", RegexOptions.Compiled);
        }

        private bool IsKeyValueFormat(string[] lines)
        {
            if (lines.Length == 0) return false;

            // 1行目が "Key,ja-JP" のようなヘッダー
            var first = lines[0].Split(',');
            if (first.Length >= 2 && first[0].Trim() == "Key" && IsCultureCode(first[1].Trim()))
                return true;

            // 2行目が "key,value" のような形式かどうか
            if (lines.Length > 1)
            {
                var second = lines[1].Split(',');
                if (second.Length >= 2 && !string.IsNullOrWhiteSpace(second[0]))
                    return true;
            }
            return false;
        }

        private Dictionary<string, string> LoadCsvToDictionary(string filePath ,Dictionary<string,string> baseResource)
        {
            var dict = new Dictionary<string, string>();
            string[] lines;

            if (string.IsNullOrEmpty(filePath))
            {
                var text = Clipboard.GetText().TrimEnd('\r', '\n');
                lines = text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            }
            else
                lines = File.ReadAllLines(filePath, Encoding.UTF8);

            if (IsKeyValueFormat(lines))
            {
                // ヘッダー判定（1行目が "Key,<カルチャコード>" ならスキップ）
                if (lines.Length > 0)
                {
                    var first = lines[0].Split(',');
                    if (first.Length >= 2 && first[0].Trim() == "Key" && IsCultureCode(first[1].Trim()))
                        lines = lines.Skip(1).ToArray();
                }

                foreach (var line in lines)
                {
                    var parts = line.Split(new char[] { ',' }, 2);
                    if (parts.Length == 2)
                    {
                        string key = parts[0].Trim();
                        string value = parts[1].Trim();
                        if (!string.IsNullOrEmpty(key))
                            dict[key] = value;
                    }
                }
            }
            else
            {
                //値のみ
                var baseKeys = baseResource.Keys.ToArray();
                if (baseKeys.Length != lines.Length)
                {
                    MessageBox.Show(
                        loader.GetString("TargetCountMismatchWithDefinitions"),
                        loader.GetString("UnableToRetrieve"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Exclamation
                    );
                    return null;
                }
                int iMax = baseKeys.Length;
                for (int i = 0; i < iMax; i++)
                {
                    string key = baseKeys[i];
                    string value = string.IsNullOrWhiteSpace(lines[i]) ? "TODO" : lines[i];
                    dict[baseKeys[i]] = value;
                }
            }
            return dict;
        }

        private void MovedResourceFolder_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (string.IsNullOrEmpty(textBox.Text) && textBox.Tag == null)
                {
                    if (textBox.Name == "MovedResourceFolder")
                        textBox.Text = resFolderName;
                    else
                        textBox.Text = resName;

                }
                textBox.SelectAll();
            }
        }
        private void MovedResourceFolder_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox && !textBox.IsKeyboardFocusWithin)
            {
                textBox.Focus();       // フォーカスを先に奪う
                e.Handled = true;      // キャレット移動を防ぐ
            }
        }

        private void MovedResourceFolder_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if(string.IsNullOrEmpty(textBox.Text))
                {
                    textBox.Tag = string.Empty;
                }
            }
        }

        //再配置実行
        private void RelocateResource_Click(object sender, RoutedEventArgs e)
        {
            string rootResourceFolder = string.Empty;
            string resourceBaseName = string.Empty;
            string resourceType = string.Empty;
            string tmp;
            string msg = string.Empty;
            bool isUWP = false;
            string fileName;

            var outList = new Dictionary<string, string>();
            var inList = new Dictionary<string, string>();
            foreach (var lo in LanguageOptions)
            {
                if (lo.IsSelected)
                    outList.Add(lo.DisplayName, "");
            }
            if (outList.Count == 0)
            {
                MessageBox.Show(
                    loader.GetString("SelectLanguageForRelocation"),
                    loader.GetString("LanguageRelocationRequired"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation
                );
                return;
            }

            tmp = NewFolderTextBox.Text;
            if (string.IsNullOrEmpty(tmp))
                msg = loader.GetString("RelocationRootFolderNotSet");
            else if (!Directory.Exists(tmp))
                msg = loader.GetString("RelocationRootFolderDoesNotExist");
            if (string.IsNullOrEmpty(msg))
            {
                rootResourceFolder = Path.GetFullPath(Path.Combine(tmp, MovedResourceFolder.Text));
                if (!IsValidPathFormat(rootResourceFolder))
                    msg = loader.GetString("InvalidRelocationFolderName");
                else
                {
                    resourceBaseName = MovedResourceName.Text;
                    if (!IsValidFilenameFormat(resourceBaseName))
                        msg = loader.GetString("InvalidRelocationFileName");
                    else if (ResExtension.SelectedIndex == -1)
                        msg = loader.GetString("RelocationFormatNotSelected");

                }
            }
            if (!string.IsNullOrEmpty(msg))
            {
                MessageBox.Show(
                    msg,
                    loader.GetString("RelocationDefinitionError"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation
                );
                return;
            }
            if (ResExtension.SelectedItem is ComboBoxItem item)
            {
                resourceType = item.Tag as string;
                if (resourceType == ".resw")
                {
                    resourceBaseName += resourceType;
                    resourceType = "";
                    isUWP = true;
                }
            }
            else return;

            //処理開始
            //ファイルネームの作成

            try
            {
                //リソース取得
                var allResource = ResourceCacheController.Get();
                if (allResource == null)
                    allResource = ResourceCacheController.Get(baseFolderPath, resFolderName, DevelopType);

                //リソースフォルダの作成
                Directory.CreateDirectory(rootResourceFolder);

                //国別に分解してファイル書き込み
                foreach (var lang in outList.Keys.ToList())
                {
                    if (isUWP)
                    {
                        var fullPath = Path.Combine(rootResourceFolder, lang);
                        Directory.CreateDirectory(fullPath);
                        outList[lang] = Path.Combine(fullPath, resourceBaseName);
                    }
                    else
                    {
                        fileName = Path.Combine(rootResourceFolder, $"{resourceBaseName}.{lang}{resourceType}");
                        outList[lang] = fileName;
                    }
                    //取込みファイル
                    if(this.isUWP)
                        inList[lang] = Path.Combine(baseFolderPath, resFolderName, lang, resName + "." + resSuffix);
                    else
                        inList[lang] = Path.Combine(baseFolderPath, resFolderName, $"{resName}.{lang}.{resSuffix}");
                    //ファイルコピー

                    File.Copy(inList[lang], outList[lang], overwrite: true);
                }
                string loaderGuide;
                if (isUWP)
                    loaderGuide = $"Windows.ApplicationModel.Resources.ResourceLoader loader = ResourceLoader.GetForViewIndependentUse();";

                else
                    loaderGuide = $"System.Resources.ResourceManager loader =  new ResourceManager(\"{Path.GetFileName(NewFolderTextBox.Text)}.{MovedResourceFolder.Text}.{resourceBaseName}\", GetType().Assembly);";

                LoaderGuideText.Text = loaderGuide.Replace("..", ".");
                //元リソース削除

            }
            catch (Exception ex)
            {
                msg = string.Format(loader.GetString("RelocationErrorOccurred"), ex.Message);
                MessageBox.Show(
                    msg,
                    loader.GetString("RelocationRuntimeError"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation
                );
                return;
            }

        }

        private bool IsValidPathFormat(string path)
        {
            // Windows用の簡易チェック（: や * などを含まない）
            var invalidChars = Path.GetInvalidPathChars();
            return !string.IsNullOrWhiteSpace(path) &&
                   !path.Any(c => invalidChars.Contains(c));
        }

        private bool IsValidFilenameFormat(string path)
        {
            // Windows用の簡易チェック（: や * などを含まない）
            var invalidChars = Path.GetInvalidFileNameChars();
            return !string.IsNullOrWhiteSpace(path) &&
                   !path.Any(c => invalidChars.Contains(c));
        }

        private void SelectAllLanguage(object sender, RoutedEventArgs e)
        {
            foreach (var lo in LanguageOptions)
            {
                lo.IsSelected = true;
            }
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;

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

    public class LanguageOption : INotifyPropertyChanged
    {
        public string DisplayName { get; internal set; } = string.Empty;
        public string CultureCode { get; internal set; } = string.Empty;

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged(nameof(IsEnabled));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
