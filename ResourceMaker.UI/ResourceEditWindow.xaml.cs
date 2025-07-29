using GalaSoft.MvvmLight.Command;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
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
        public List<string> allCodes = new();

        private string baseFolderPath = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;
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

        public ObservableCollection<LanguageEntry> LanguageEntries { get; } = new();

        public ResourceEditWindow()
        {
            InitializeComponent();
            this.DataContext = this; // ← これが必要！

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
                   .Distinct()
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
            else
            {
                //
                this.SelectedTextname.Text = this.LineText;
                LanguageEntries.Clear();
                foreach (var code in allCodes.Distinct())
                {
                    LanguageEntries.Add(new LanguageEntry(code));
                }
            }

        }
        private void SaveResource_Click(object sender, RoutedEventArgs e)
        {
                
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

            public event PropertyChangedEventHandler? PropertyChanged;
        }
    }
}
