using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using WuwaModModifier.Common;
using WuwaModModifier.ViewModels;

namespace WuwaModModifier
{
    public partial class VersionSyncWindow : Window
    {
        private enum CurrentDifferenceApplySource
        {
            Old,
            New
        }

        private static readonly Brush OldDiffBrush = CreateFrozenBrush(Color.FromRgb(0xF8, 0xE1, 0xD6));
        private static readonly Brush NewDiffBrush = CreateFrozenBrush(Color.FromRgb(0xD8, 0xEF, 0xEA));
        private static readonly Brush ResultDiffBrush = CreateFrozenBrush(Color.FromRgb(0xF5, 0xE7, 0xB5));
        private static readonly Brush OldDiffLineBrush = CreateFrozenBrush(Color.FromArgb(0x70, 0xF8, 0xE1, 0xD6));
        private static readonly Brush NewDiffLineBrush = CreateFrozenBrush(Color.FromArgb(0x70, 0xD8, 0xEF, 0xEA));
        private static readonly Brush ResultDiffLineBrush = CreateFrozenBrush(Color.FromArgb(0x70, 0xF5, 0xE7, 0xB5));

        private bool _isUpdatingComparisonEditorsFromViewModel;
        private bool _isUpdatingViewModelFromResultEditor;
        private bool _isSynchronizingComparisonScroll;
        private readonly Dictionary<RichTextBox, IReadOnlyList<int>> _comparisonDifferenceLineIndices;
        private readonly Dictionary<RichTextBox, IReadOnlyDictionary<int, int>> _comparisonDifferenceOrdinalsByLineIndex;
        private int _comparisonTotalDifferenceCount;
        private RichTextBox? _activeComparisonEditor;

        public VersionSyncWindowViewModel ViewModel { get; }

        public VersionSyncWindow(string? initialImportedModDirectoryPath = null)
        {
            InitializeComponent();
            _comparisonDifferenceLineIndices = new Dictionary<RichTextBox, IReadOnlyList<int>>();
            _comparisonDifferenceOrdinalsByLineIndex = new Dictionary<RichTextBox, IReadOnlyDictionary<int, int>>();
            ViewModel = new VersionSyncWindowViewModel(initialImportedModDirectoryPath);
            DataContext = ViewModel;
            Loaded += VersionSyncWindow_Loaded;
            Closed += VersionSyncWindow_Closed;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void VersionSyncWindow_Loaded(object sender, RoutedEventArgs e)
        {
            AttachComparisonScrollSynchronization();
            _activeComparisonEditor = rtbResultConfigText;
            RenderComparisonEditors(resetScrollPosition: true);
        }

        private void VersionSyncWindow_Closed(object? sender, EventArgs e)
        {
            Loaded -= VersionSyncWindow_Loaded;
            Closed -= VersionSyncWindow_Closed;
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            DetachComparisonScrollSynchronization();
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VersionSyncWindowViewModel.OldConfigText) ||
                e.PropertyName == nameof(VersionSyncWindowViewModel.NewConfigText))
            {
                RenderComparisonEditors(resetScrollPosition: true);
                return;
            }

            if (e.PropertyName == nameof(VersionSyncWindowViewModel.ResultConfigText) &&
                !_isUpdatingViewModelFromResultEditor)
            {
                RenderComparisonEditors(resetScrollPosition: false);
            }
        }

        private void btnAddManualPairing_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.CanOpenManualPairing)
            {
                return;
            }

            var dialog = new ManualPairingWindow(ViewModel.ManualPairingCandidates)
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true || dialog.SelectedOldCandidate == null || dialog.SelectedNewCandidate == null)
            {
                return;
            }

            if (!ViewModel.TryAddManualPairing(dialog.SelectedOldCandidate.FullPath, dialog.SelectedNewCandidate.FullPath, out var errorMessage))
            {
                MessageBox.Show(errorMessage, "版本同步", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void PairingJobsGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var row = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject);
            if (row?.Item != null)
            {
                row.IsSelected = true;
            }
        }

        private void rtbResultConfigText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingComparisonEditorsFromViewModel)
            {
                return;
            }

            _isUpdatingViewModelFromResultEditor = true;
            try
            {
                var editorText = ReadEditorText(rtbResultConfigText);
                if (!string.Equals(ViewModel.ResultConfigText, editorText, StringComparison.Ordinal))
                {
                    ViewModel.ResultConfigText = editorText;
                }
            }
            finally
            {
                _isUpdatingViewModelFromResultEditor = false;
            }
        }

        private void rtbResultConfigText_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingViewModelFromResultEditor)
            {
                return;
            }

            RenderResultComparisonEditor(
                preserveScrollPosition: true,
                preferredCaretLineIndex: GetCurrentLineIndex(rtbResultConfigText));
        }

        private void ComparisonEditor_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is RichTextBox editor)
            {
                _activeComparisonEditor = editor;
                RefreshDifferenceNavigationButtons();
            }
        }

        private void ComparisonEditor_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (sender is RichTextBox editor)
            {
                _activeComparisonEditor = editor;
                RefreshDifferenceNavigationButtons();
            }
        }

        private void btnPreviousDifference_Click(object sender, RoutedEventArgs e)
        {
            NavigateToDifference(moveNext: false);
        }

        private void btnNextDifference_Click(object sender, RoutedEventArgs e)
        {
            NavigateToDifference(moveNext: true);
        }

        private void btnApplyOldDifference_Click(object sender, RoutedEventArgs e)
        {
            ApplyCurrentDifference(CurrentDifferenceApplySource.Old);
        }

        private void btnApplyNewDifference_Click(object sender, RoutedEventArgs e)
        {
            ApplyCurrentDifference(CurrentDifferenceApplySource.New);
        }

        private void RenderComparisonEditors(bool resetScrollPosition)
        {
            var synchronizedState = TextDiffHighlighter.BuildSynchronizedState(
                ViewModel.OldConfigText,
                ViewModel.NewConfigText,
                ViewModel.ResultConfigText);
            _comparisonTotalDifferenceCount = synchronizedState.TotalDifferenceCount;

            RenderComparisonEditor(
                rtbOldConfigText,
                OldDiffBrush,
                synchronizedState.OldEditor.Lines,
                synchronizedState.OldEditor.DifferenceLineIndices,
                synchronizedState.OldEditor.DifferenceOrdinalByLineIndex);
            RenderComparisonEditor(
                rtbNewConfigText,
                NewDiffBrush,
                synchronizedState.NewEditor.Lines,
                synchronizedState.NewEditor.DifferenceLineIndices,
                synchronizedState.NewEditor.DifferenceOrdinalByLineIndex);

            if (resetScrollPosition)
            {
                RenderResultComparisonEditor(synchronizedState.ResultEditor, preserveScrollPosition: false);
                SynchronizeComparisonScrollOffsets(0, 0, null);
                RefreshDifferenceNavigationButtons();
                return;
            }

            RenderResultComparisonEditor(synchronizedState.ResultEditor, preserveScrollPosition: true);
            RefreshDifferenceNavigationButtons();
        }

        private void RenderResultComparisonEditor(bool preserveScrollPosition, int? preferredCaretLineIndex = null)
        {
            var synchronizedState = TextDiffHighlighter.BuildSynchronizedState(
                ViewModel.OldConfigText,
                ViewModel.NewConfigText,
                ViewModel.ResultConfigText);
            _comparisonTotalDifferenceCount = synchronizedState.TotalDifferenceCount;
            RenderResultComparisonEditor(synchronizedState.ResultEditor, preserveScrollPosition, preferredCaretLineIndex);
            RefreshDifferenceNavigationButtons();
        }

        private void RenderResultComparisonEditor(
            TextDiffSynchronizedEditorState editorState,
            bool preserveScrollPosition,
            int? preferredCaretLineIndex = null)
        {
            var scrollPosition = preserveScrollPosition ? GetCurrentComparisonScrollPosition() : null;
            RenderComparisonEditor(
                rtbResultConfigText,
                ResultDiffBrush,
                editorState.Lines,
                editorState.DifferenceLineIndices,
                editorState.DifferenceOrdinalByLineIndex);

            if (preferredCaretLineIndex.HasValue)
            {
                RestoreCaretLineIndex(rtbResultConfigText, preferredCaretLineIndex.Value);
            }

            if (scrollPosition.HasValue)
            {
                SynchronizeComparisonScrollOffsets(scrollPosition.Value.HorizontalOffset, scrollPosition.Value.VerticalOffset, null);
            }
        }

        private void RenderComparisonEditor(
            RichTextBox editor,
            Brush diffBrush,
            IReadOnlyList<TextDiffHighlightLine> lines,
            IReadOnlyList<int> differenceLineIndices,
            IReadOnlyDictionary<int, int> differenceOrdinalByLineIndex)
        {
            _comparisonDifferenceLineIndices[editor] = differenceLineIndices;
            _comparisonDifferenceOrdinalsByLineIndex[editor] = differenceOrdinalByLineIndex;

            try
            {
                _isUpdatingComparisonEditorsFromViewModel = true;
                editor.Document = BuildDiffDocument(lines, diffBrush);
            }
            finally
            {
                _isUpdatingComparisonEditorsFromViewModel = false;
            }
        }

        private void AttachComparisonScrollSynchronization()
        {
            DetachComparisonScrollSynchronization();

            rtbOldConfigText.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(ComparisonScrollViewer_ScrollChanged), true);
            rtbNewConfigText.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(ComparisonScrollViewer_ScrollChanged), true);
            rtbResultConfigText.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(ComparisonScrollViewer_ScrollChanged), true);
        }

        private void DetachComparisonScrollSynchronization()
        {
            rtbOldConfigText.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(ComparisonScrollViewer_ScrollChanged));
            rtbNewConfigText.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(ComparisonScrollViewer_ScrollChanged));
            rtbResultConfigText.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(ComparisonScrollViewer_ScrollChanged));
        }

        private void ComparisonScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isSynchronizingComparisonScroll ||
                (Math.Abs(e.HorizontalChange) < double.Epsilon && Math.Abs(e.VerticalChange) < double.Epsilon))
            {
                return;
            }

            var sourceViewer = e.OriginalSource as ScrollViewer;
            if (sourceViewer == null && sender is RichTextBox sourceEditor)
            {
                sourceViewer = GetScrollViewer(sourceEditor);
            }

            if (sourceViewer == null)
            {
                return;
            }

            SynchronizeComparisonScrollOffsets(sourceViewer.HorizontalOffset, sourceViewer.VerticalOffset, sourceViewer);
        }

        private void SynchronizeComparisonScrollOffsets(double horizontalOffset, double verticalOffset, ScrollViewer? sourceViewer)
        {
            try
            {
                _isSynchronizingComparisonScroll = true;

                foreach (var targetViewer in new[]
                         {
                             GetScrollViewer(rtbOldConfigText),
                             GetScrollViewer(rtbNewConfigText),
                             GetScrollViewer(rtbResultConfigText)
                         })
                {
                    if (targetViewer == null || ReferenceEquals(targetViewer, sourceViewer))
                    {
                        continue;
                    }

                    targetViewer.ScrollToHorizontalOffset(horizontalOffset);
                    targetViewer.ScrollToVerticalOffset(verticalOffset);
                }
            }
            finally
            {
                _isSynchronizingComparisonScroll = false;
            }
        }

        private ComparisonScrollPosition? GetCurrentComparisonScrollPosition()
        {
            var referenceViewer = GetScrollViewer(rtbResultConfigText) ??
                GetScrollViewer(rtbOldConfigText) ??
                GetScrollViewer(rtbNewConfigText);
            if (referenceViewer == null)
            {
                return null;
            }

            return new ComparisonScrollPosition(referenceViewer.HorizontalOffset, referenceViewer.VerticalOffset);
        }

        private void RefreshDifferenceNavigationButtons()
        {
            var editor = GetActiveComparisonEditor();
            if (editor == null || _comparisonTotalDifferenceCount == 0)
            {
                btnPreviousDifference.IsEnabled = false;
                btnNextDifference.IsEnabled = false;
                SetCurrentDifferenceApplyButtonsEnabled(false);
                txtDifferencePosition.Text = "无差异";
                return;
            }

            btnPreviousDifference.IsEnabled = true;
            btnNextDifference.IsEnabled = true;
            var currentOrdinal = GetCurrentDifferenceOrdinal(editor);
            SetCurrentDifferenceApplyButtonsEnabled(currentOrdinal.HasValue);
            txtDifferencePosition.Text = currentOrdinal.HasValue
                ? $"当前第 {currentOrdinal.Value} 处 / 共 {_comparisonTotalDifferenceCount} 处差异"
                : $"当前未停留在差异处 / 共 {_comparisonTotalDifferenceCount} 处差异";
        }

        private void ApplyCurrentDifference(CurrentDifferenceApplySource applySource)
        {
            var sourceEditor = GetActiveComparisonEditor();
            var currentOrdinal = GetCurrentDifferenceOrdinal(sourceEditor);
            if (!currentOrdinal.HasValue)
            {
                RefreshDifferenceNavigationButtons();
                return;
            }

            var synchronizedState = TextDiffHighlighter.BuildSynchronizedState(
                ViewModel.OldConfigText,
                ViewModel.NewConfigText,
                ViewModel.ResultConfigText);
            var resultLineIndex = TextDiffHighlighter.FindLineIndexByDifferenceOrdinal(
                synchronizedState.ResultEditor.DifferenceOrdinalByLineIndex,
                currentOrdinal.Value);
            IReadOnlyDictionary<int, string?> sourceLineTextByResultLineIndex = applySource switch
            {
                CurrentDifferenceApplySource.Old => TextDiffHighlighter.BuildSourceLineTextByReferenceLineIndexForReplacement(
                    ViewModel.OldConfigText,
                    ViewModel.ResultConfigText),
                CurrentDifferenceApplySource.New => TextDiffHighlighter.BuildSourceLineTextByReferenceLineIndexForReplacement(
                    ViewModel.NewConfigText,
                    ViewModel.ResultConfigText),
                _ => throw new InvalidOperationException("Unknown difference apply source.")
            };

            if (!resultLineIndex.HasValue ||
                resultLineIndex.Value < 0 ||
                resultLineIndex.Value >= synchronizedState.ResultEditor.Lines.Count ||
                !TryResolveResultLineReplacement(
                    sourceLineTextByResultLineIndex,
                    resultLineIndex.Value,
                    out var replacementLineText,
                    out var removeResultLine) ||
                !TryUpdateResultTextAtLineIndex(
                    ViewModel.ResultConfigText,
                    resultLineIndex.Value,
                    replacementLineText,
                    removeResultLine,
                    out var updatedResultText))
            {
                RefreshDifferenceNavigationButtons();
                return;
            }

            var preferredCaretLineIndex = removeResultLine
                ? Math.Max(0, resultLineIndex.Value - 1)
                : resultLineIndex.Value;

            _activeComparisonEditor = rtbResultConfigText;
            ViewModel.ResultConfigText = updatedResultText;

            rtbResultConfigText.Focus();
            RestoreCaretLineIndex(rtbResultConfigText, preferredCaretLineIndex);
            GetParagraphAtOrBeforeLineIndex(rtbResultConfigText.Document, preferredCaretLineIndex)?.BringIntoView();
            RefreshDifferenceNavigationButtons();
        }

        private void NavigateToDifference(bool moveNext)
        {
            if (_comparisonTotalDifferenceCount == 0)
            {
                RefreshDifferenceNavigationButtons();
                return;
            }

            var sourceEditor = GetActiveComparisonEditor();
            var targetOrdinal = GetNavigationTargetOrdinal(sourceEditor, moveNext);
            if (!TryGetDifferenceLineIndexByOrdinal(rtbResultConfigText, targetOrdinal, out var targetLineIndex))
            {
                RefreshDifferenceNavigationButtons();
                return;
            }

            var paragraph = GetParagraphAtLineIndex(rtbResultConfigText.Document, targetLineIndex);
            if (paragraph == null)
            {
                RefreshDifferenceNavigationButtons();
                return;
            }

            _activeComparisonEditor = rtbResultConfigText;
            rtbResultConfigText.Focus();
            rtbResultConfigText.CaretPosition = paragraph.ContentStart;
            paragraph.BringIntoView();
            RefreshDifferenceNavigationButtons();
        }

        private int GetNavigationTargetOrdinal(RichTextBox? sourceEditor, bool moveNext)
        {
            if (_comparisonTotalDifferenceCount <= 1)
            {
                return 1;
            }

            if (sourceEditor == null ||
                !_comparisonDifferenceLineIndices.TryGetValue(sourceEditor, out var differenceLineIndices) ||
                !_comparisonDifferenceOrdinalsByLineIndex.TryGetValue(sourceEditor, out var differenceOrdinalByLineIndex) ||
                differenceLineIndices.Count == 0)
            {
                return moveNext ? 1 : _comparisonTotalDifferenceCount;
            }

            var currentLineIndex = GetCurrentLineIndex(sourceEditor);
            return TextDiffHighlighter.FindNavigationTargetOrdinal(
                       differenceLineIndices,
                       differenceOrdinalByLineIndex,
                       currentLineIndex,
                       moveNext,
                       _comparisonTotalDifferenceCount) ??
                   (moveNext ? 1 : _comparisonTotalDifferenceCount);
        }

        private bool TryGetDifferenceLineIndexByOrdinal(RichTextBox editor, int differenceOrdinal, out int lineIndex)
        {
            lineIndex = 0;
            if (!_comparisonDifferenceOrdinalsByLineIndex.TryGetValue(editor, out var differenceOrdinalsByLineIndex))
            {
                return false;
            }

            var result = TextDiffHighlighter.FindLineIndexByDifferenceOrdinal(differenceOrdinalsByLineIndex, differenceOrdinal);
            if (!result.HasValue)
            {
                return false;
            }

            lineIndex = result.Value;
            return true;
        }

        private int? GetCurrentDifferenceOrdinal(RichTextBox? editor)
        {
            if (editor == null ||
                !_comparisonDifferenceOrdinalsByLineIndex.TryGetValue(editor, out var differenceOrdinalByLineIndex))
            {
                return null;
            }

            var currentLineIndex = GetCurrentLineIndex(editor);
            return differenceOrdinalByLineIndex.TryGetValue(currentLineIndex, out var ordinal)
                ? ordinal
                : (int?)null;
        }

        private void SetCurrentDifferenceApplyButtonsEnabled(bool isEnabled)
        {
            btnApplyOldDifference.IsEnabled = isEnabled;
            btnApplyNewDifference.IsEnabled = isEnabled;
        }

        private static bool TryResolveResultLineReplacement(
            IReadOnlyDictionary<int, string?> sourceLineTextByResultLineIndex,
            int resultLineIndex,
            out string? replacementLineText,
            out bool removeResultLine)
        {
            return TryResolveReplacementFromMap(
                sourceLineTextByResultLineIndex,
                resultLineIndex,
                out replacementLineText,
                out removeResultLine);
        }

        private static bool TryResolveReplacementFromMap(
            IReadOnlyDictionary<int, string?> lineTextByResultLineIndex,
            int resultLineIndex,
            out string? replacementLineText,
            out bool removeResultLine)
        {
            if (!lineTextByResultLineIndex.TryGetValue(resultLineIndex, out replacementLineText))
            {
                removeResultLine = false;
                return false;
            }

            removeResultLine = replacementLineText == null;
            return true;
        }

        private static bool TryUpdateResultTextAtLineIndex(
            string resultText,
            int lineIndex,
            string? replacementLineText,
            bool removeResultLine,
            out string updatedResultText)
        {
            updatedResultText = resultText;

            var lines = SplitTextLinesForEditing(resultText);
            if (lineIndex < 0 || lineIndex >= lines.Count)
            {
                return false;
            }

            if (removeResultLine)
            {
                lines.RemoveAt(lineIndex);
            }
            else if (replacementLineText != null)
            {
                lines[lineIndex] = replacementLineText;
            }
            else
            {
                return false;
            }

            if (lines.Count == 0)
            {
                updatedResultText = string.Empty;
                return true;
            }

            var lineEnding = DetectLineEnding(resultText);
            updatedResultText = string.Join(lineEnding, lines);
            if (HasTrailingLineBreak(resultText))
            {
                updatedResultText += lineEnding;
            }

            return true;
        }

        private static List<string> SplitTextLinesForEditing(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new List<string>();
            }

            var normalized = text
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');
            var lines = normalized.Split('\n').ToList();
            if (lines.Count > 0 && lines[^1].Length == 0)
            {
                lines.RemoveAt(lines.Count - 1);
            }

            return lines;
        }

        private static string DetectLineEnding(string text)
        {
            if (text.Contains("\r\n", StringComparison.Ordinal))
            {
                return "\r\n";
            }

            if (text.Contains('\n'))
            {
                return "\n";
            }

            if (text.Contains('\r'))
            {
                return "\r";
            }

            return Environment.NewLine;
        }

        private static bool HasTrailingLineBreak(string text)
        {
            return text.EndsWith("\r\n", StringComparison.Ordinal) ||
                text.EndsWith("\n", StringComparison.Ordinal) ||
                text.EndsWith("\r", StringComparison.Ordinal);
        }

        private RichTextBox? GetActiveComparisonEditor()
        {
            if (_activeComparisonEditor != null)
            {
                return _activeComparisonEditor;
            }

            return rtbResultConfigText;
        }

        private static int GetCurrentLineIndex(RichTextBox editor)
        {
            var currentParagraph = editor.CaretPosition?.Paragraph;
            if (currentParagraph == null)
            {
                return 0;
            }

            var lineIndex = 0;
            for (Block? block = editor.Document.Blocks.FirstBlock; block != null; block = block.NextBlock)
            {
                if (ReferenceEquals(block, currentParagraph))
                {
                    return lineIndex;
                }

                lineIndex++;
            }

            return Math.Max(0, lineIndex - 1);
        }

        private static Paragraph? GetParagraphAtLineIndex(FlowDocument document, int lineIndex)
        {
            var currentLineIndex = 0;
            for (Block? block = document.Blocks.FirstBlock; block != null; block = block.NextBlock)
            {
                if (currentLineIndex == lineIndex)
                {
                    return block as Paragraph;
                }

                currentLineIndex++;
            }

            return null;
        }

        private static void RestoreCaretLineIndex(RichTextBox editor, int lineIndex)
        {
            var paragraph = GetParagraphAtOrBeforeLineIndex(editor.Document, lineIndex);
            if (paragraph != null)
            {
                editor.CaretPosition = paragraph.ContentStart;
            }
        }

        private static Paragraph? GetParagraphAtOrBeforeLineIndex(FlowDocument document, int lineIndex)
        {
            Paragraph? lastParagraph = null;
            var currentLineIndex = 0;
            for (Block? block = document.Blocks.FirstBlock; block != null; block = block.NextBlock)
            {
                if (block is not Paragraph paragraph)
                {
                    currentLineIndex++;
                    continue;
                }

                lastParagraph = paragraph;
                if (currentLineIndex >= lineIndex)
                {
                    return paragraph;
                }

                currentLineIndex++;
            }

            return lastParagraph;
        }

        private static FlowDocument BuildDiffDocument(IReadOnlyList<TextDiffHighlightLine> lines, Brush diffBrush)
        {
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
                document.Blocks.Add(BuildParagraph(
                    line,
                    diffBrush,
                    ReferenceEquals(diffBrush, OldDiffBrush)
                        ? OldDiffLineBrush
                        : ReferenceEquals(diffBrush, NewDiffBrush)
                            ? NewDiffLineBrush
                            : ResultDiffLineBrush));
            }

            if (document.Blocks.Count == 0)
            {
                document.Blocks.Add(new Paragraph(new Run(string.Empty))
                {
                    Margin = new Thickness(0),
                    Padding = new Thickness(0)
                });
            }

            return document;
        }

        private static Paragraph BuildParagraph(TextDiffHighlightLine line, Brush diffBrush, Brush lineDiffBrush)
        {
            var paragraph = new Paragraph
            {
                Margin = new Thickness(0),
                Padding = new Thickness(0)
            };

            if (line.HasDifference)
            {
                paragraph.Background = lineDiffBrush;
            }

            if (line.Spans.Count == 0)
            {
                paragraph.Inlines.Add(new Run(line.Text));
            }
            else
            {
                foreach (var span in line.Spans)
                {
                    if (span.Text.Length == 0)
                    {
                        continue;
                    }

                    var run = new Run(span.Text);
                    if (span.IsDifferent)
                    {
                        run.Background = diffBrush;
                    }

                    paragraph.Inlines.Add(run);
                }
            }

            if (paragraph.Inlines.FirstInline == null)
            {
                paragraph.Inlines.Add(new Run(string.Empty));
            }

            return paragraph;
        }

        private static string ReadEditorText(RichTextBox editor)
        {
            var text = new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text;
            return text.EndsWith("\r\n", StringComparison.Ordinal)
                ? text[..^2]
                : text;
        }

        private static ScrollViewer? GetScrollViewer(RichTextBox editor)
        {
            return FindDescendant<ScrollViewer>(editor);
        }

        private static Brush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static T? FindDescendant<T>(DependencyObject? current)
            where T : DependencyObject
        {
            if (current == null)
            {
                return null;
            }

            var childCount = VisualTreeHelper.GetChildrenCount(current);
            for (var childIndex = 0; childIndex < childCount; childIndex++)
            {
                var child = VisualTreeHelper.GetChild(current, childIndex);
                if (child is T typedChild)
                {
                    return typedChild;
                }

                var descendant = FindDescendant<T>(child);
                if (descendant != null)
                {
                    return descendant;
                }
            }

            return null;
        }

        private static T? FindAncestor<T>(DependencyObject? current)
            where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T typedAncestor)
                {
                    return typedAncestor;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private readonly record struct ComparisonScrollPosition(double HorizontalOffset, double VerticalOffset);
    }
}