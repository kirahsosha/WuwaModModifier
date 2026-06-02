using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WuwaModModifier.Data.ViewModels;
using WuwaModModifier.ViewModels;

namespace WuwaModModifier
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly Brush _toggleHighlightBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF4D6"));
        private readonly Brush _parameterHighlightBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DDF2FF"));
        private readonly Brush _visibilityHighlightBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DFF3E4"));
        private bool _isUpdatingEditorFromViewModel;
        private bool _isUpdatingViewModelFromEditor;

        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = new MainViewModel();
            DataContext = ViewModel;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            RenderRawConfigEditor();
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isUpdatingViewModelFromEditor)
            {
                return;
            }

            if (e.PropertyName == nameof(MainViewModel.RawConfigNavigateRequestVersion))
            {
                QueueRawConfigNavigation(ViewModel.RawConfigNavigateLine);
                return;
            }

            if (e.PropertyName == nameof(MainViewModel.RawConfigEditorText) ||
                e.PropertyName == nameof(MainViewModel.RawConfigHighlights) ||
                e.PropertyName == nameof(MainViewModel.SelectedConfigPath))
            {
                RenderRawConfigEditor();
            }
        }

        private void rtbConfigEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingEditorFromViewModel)
            {
                return;
            }

            try
            {
                _isUpdatingViewModelFromEditor = true;
                ViewModel.RawConfigEditorText = ReadEditorText();
            }
            finally
            {
                _isUpdatingViewModelFromEditor = false;
            }
        }

        private void RenderRawConfigEditor()
        {
            try
            {
                _isUpdatingEditorFromViewModel = true;
                var editorText = ViewModel.RawConfigEditorText;
                var lines = editorText
                    .Replace("\r\n", "\n")
                    .Replace('\r', '\n')
                    .Split('\n');
                var lineHighlights = BuildLineHighlightMap(ViewModel.RawConfigHighlights);

                var document = new FlowDocument
                {
                    PagePadding = new Thickness(0),
                    ColumnWidth = 10000,
                    PageWidth = 10000,
                    FontFamily = new FontFamily("Consolas")
                };

                var paragraphStyle = new Style(typeof(Paragraph));
                paragraphStyle.Setters.Add(new Setter(Block.MarginProperty, new Thickness(0)));
                paragraphStyle.Setters.Add(new Setter(Block.PaddingProperty, new Thickness(0)));
                document.Resources.Add(typeof(Paragraph), paragraphStyle);

                foreach (var line in lines)
                {
                    var paragraph = new Paragraph(new Run(line))
                    {
                        Margin = new Thickness(0),
                        Padding = new Thickness(0)
                    };

                    var lineNumber = document.Blocks.Count + 1;
                    if (lineHighlights.TryGetValue(lineNumber, out var kind))
                    {
                        paragraph.Background = GetHighlightBrush(kind);
                    }

                    document.Blocks.Add(paragraph);
                }

                if (document.Blocks.Count == 0)
                {
                    document.Blocks.Add(new Paragraph(new Run(string.Empty))
                    {
                        Margin = new Thickness(0),
                        Padding = new Thickness(0)
                    });
                }

                rtbConfigEditor.Document = document;
            }
            finally
            {
                _isUpdatingEditorFromViewModel = false;
            }
        }

        private void QueueRawConfigNavigation(int lineNumber)
        {
            if (lineNumber <= 0)
            {
                return;
            }

            Dispatcher.InvokeAsync(
                () => NavigateRawConfigEditorToLine(lineNumber),
                DispatcherPriority.Background);
        }

        private void NavigateRawConfigEditorToLine(int lineNumber)
        {
            if (lineNumber <= 0)
            {
                return;
            }

            var paragraph = GetParagraphByLineNumber(rtbConfigEditor.Document, lineNumber);
            if (paragraph == null)
            {
                return;
            }

            var position = paragraph.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
            rtbConfigEditor.Focus();
            rtbConfigEditor.Selection.Select(position, position);
            rtbConfigEditor.CaretPosition = position;
            paragraph.BringIntoView();
        }

        private static Paragraph? GetParagraphByLineNumber(FlowDocument document, int lineNumber)
        {
            if (lineNumber <= 0)
            {
                return null;
            }

            var currentLine = 1;
            for (var block = document.Blocks.FirstBlock; block != null; block = block.NextBlock)
            {
                if (currentLine == lineNumber)
                {
                    return block as Paragraph;
                }

                currentLine++;
            }

            return null;
        }

        private Dictionary<int, ConfigTextHighlightKind> BuildLineHighlightMap(IEnumerable<ConfigTextHighlightItem> highlights)
        {
            var result = new Dictionary<int, ConfigTextHighlightKind>();
            foreach (var highlight in highlights)
            {
                for (var line = highlight.StartLine; line <= highlight.EndLine; line++)
                {
                    if (!result.TryGetValue(line, out var existingKind) ||
                        GetHighlightPriority(highlight.Kind) > GetHighlightPriority(existingKind))
                    {
                        result[line] = highlight.Kind;
                    }
                }
            }

            return result;
        }

        private int GetHighlightPriority(ConfigTextHighlightKind kind)
        {
            return kind switch
            {
                ConfigTextHighlightKind.Visibility => 3,
                ConfigTextHighlightKind.Toggle => 2,
                _ => 1
            };
        }

        private Brush GetHighlightBrush(ConfigTextHighlightKind kind)
        {
            return kind switch
            {
                ConfigTextHighlightKind.Visibility => _visibilityHighlightBrush,
                ConfigTextHighlightKind.Toggle => _toggleHighlightBrush,
                _ => _parameterHighlightBrush
            };
        }

        private string ReadEditorText()
        {
            var text = new TextRange(rtbConfigEditor.Document.ContentStart, rtbConfigEditor.Document.ContentEnd).Text;
            return text.EndsWith("\r\n")
                ? text[..^2]
                : text;
        }

        private void tvModDirectory_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is DirectoryItemViewModel item)
            {
                ViewModel.SelectedDirectoryItem = item;
            }
            else
            {
                ViewModel.SelectedDirectoryItem = null;
            }
        }

        private void ConfigAnalysisGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var row = VisualUpwardSearch<DataGridRow>(e.OriginalSource as DependencyObject);
            if (row?.DataContext is ConfigToggleSummaryItem toggleItem)
            {
                ViewModel.RequestRawConfigNavigationForLine(toggleItem.NavigateLine);
                return;
            }

            if (row?.DataContext is ConfigParameterSummaryItem parameterItem)
            {
                ViewModel.RequestRawConfigNavigationForLine(parameterItem.NavigateLine);
                return;
            }

            if (row?.DataContext is ConfigVisibilitySummaryItem visibilityItem)
            {
                ViewModel.RequestRawConfigNavigationForLine(visibilityItem.NavigateLine);
            }
        }

        /// <summary>
        /// 右键时选中当前 TreeViewItem，并更新 ViewModel.SelectedDirectoryItem
        /// </summary>
        private void tvModDirectory_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var treeViewItem = VisualUpwardSearch<TreeViewItem>(e.OriginalSource as DependencyObject);
            if (treeViewItem != null)
            {
                treeViewItem.Focus();
                treeViewItem.IsSelected = true;

                if (treeViewItem.DataContext is DirectoryItemViewModel item)
                {
                    ViewModel.SelectedDirectoryItem = item;
                }
            }
        }

        private static T? VisualUpwardSearch<T>(DependencyObject? source) where T : DependencyObject
        {
            while (source != null && source is not T)
            {
                source = VisualTreeHelper.GetParent(source);
            }
            return source as T;
        }

        private void btnRandomLoadAllMods_Click()
        {

        }

        private void btnOpenVersionSync_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.CanOpenVersionSync)
            {
                return;
            }

            var window = new VersionSyncWindow(ViewModel.SelectedVersionSyncDirectoryPath)
            {
                Owner = this
            };

            window.ShowDialog();
        }
    }
}
