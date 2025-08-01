﻿using GalaSoft.MvvmLight.Command;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
        private string baseFolderPath = string.Empty;

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

        public ObservableCollection<LanguageOption> LanguageOptions { get; set; } = new();
        public ObservableCollection<LanguageEntry> CustomLanguages { get; } = new();

        public LanguageSelectionWindow()
        {
            InitializeComponent();
            this.DataContext = this; // ← これが必要！

        }

        public void LoadLanguageOptionsFromFolder(string basePath)
        {
            List<string> folders = new List<string>();
            var stringsRoot = Path.Combine(BaseFolderPath, "Strings");
            if (Directory.Exists(stringsRoot))
            {
                // フォルダが存在している
                var directories = Directory.GetDirectories(stringsRoot);

                foreach (var dir in directories)
                {
                    var name = Path.GetFileName(dir);
                    if (MyRegex.IsMatch(name))
                        folders.Add(name);

                }
            }
            else
            {
                // フォルダが存在していない
                Directory.CreateDirectory(stringsRoot);
                folders = new List<string>();
            }

            LanguageOptions.Clear();
            // ① 初期言語を設定
            var defaultCodes = new[] { "ja-JP", "en-US" };
            foreach (var code in defaultCodes)
            {
                LanguageOptions.Add(new LanguageOption
                {
                    DisplayName = code,
                    CultureCode = code,
                    IsSelected = false
                });
            }

            // ② folders の内容で反映
            foreach (var folderCode in folders)
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
                        IsSelected = true
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

        private async void MakeFolders_Click(object sender, RoutedEventArgs e)
        {
            await CreateLanguageCodeFoldersAsync(Path.Combine(BaseFolderPath, "Strings"));
            this.DialogResult = true;
            this.Close();
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

                if (!Directory.Exists(basePath))
                    Directory.CreateDirectory(basePath);

                allCodes = LanguageOptions.Where(o => o.IsSelected)
                    .Select(o => o.CultureCode)
                    .Concat(CustomLanguages.Select(c => c.Code))
                    .Where(code => !string.IsNullOrWhiteSpace(code))
                    .Distinct()
                    .ToList();

                var tasks = allCodes.Select(code =>
                    Task.Run(() =>
                    {
                        string folderPath = Path.Combine(basePath, code);
                        if (!Directory.Exists(folderPath))
                            Directory.CreateDirectory(folderPath);

                        //テンプレートの作成
                        string fileName = "Resources.resw";
                        string filePath = Path.Combine(folderPath, fileName);

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

                    }));

                await Task.WhenAll(tasks);
                return true;
            }
            catch
            {
                return false;
            }
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
    }
}
