using Microsoft.Extensions.DependencyInjection;
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
using WuwaModModifier.Model;
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

        private static Brush OldDiffBrush => DiffRenderingService.OldDiffBrush;
        private static Brush NewDiffBrush => DiffRenderingService.NewDiffBrush;
        private static Brush ResultDiffBrush => DiffRenderingService.ResultDiffBrush;
        private static Brush OldDiffLineBrush => DiffRenderingService.OldDiffLineBrush;
        private static Brush NewDiffLineBrush => DiffRenderingService.NewDiffLineBrush;
        private static Brush ResultDiffLineBrush => DiffRenderingService.ResultDiffLineBrush;
        private static Brush PlaceholderLineBrush => DiffRenderingService.PlaceholderLineBrush;
        private static Brush TransparentBrush => DiffRenderingService.TransparentBrush;

        private static readonly DependencyProperty IsPlaceholderParagraphProperty =
            DependencyProperty.RegisterAttached(
                "IsPlaceholderParagraph",
                typeof(bool),
                typeof(VersionSyncWindow),
                new FrameworkPropertyMetadata(false));

        private bool _isUpdatingComparisonEditorsFromViewModel;
        private bool _isUpdatingViewModelFromResultEditor;
        private bool _isSynchronizingComparisonScroll;
        private readonly Dictionary<RichTextBox, IReadOnlyList<int>> _comparisonDifferenceLineIndices;
        private readonly Dictionary<RichTextBox, IReadOnlyDictionary<int, int>> _comparisonDifferenceOrdinalsByLineIndex;
        private int _comparisonTotalDifferenceCount;
        private RichTextBox? _activeComparisonEditor;

        public VersionSyncWindowViewModel ViewModel { get; }

        public VersionSyncWindow()
        {
            InitializeComponent();
            _comparisonDifferenceLineIndices = new Dictionary<RichTextBox, IReadOnlyList<int>>();
            _comparisonDifferenceOrdinalsByLineIndex = new Dictionary<RichTextBox, IReadOnlyDictionary<int, int>>();
            ViewModel = App.ServiceProvider.GetRequiredService<VersionSyncWindowViewModel>();
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
            SelectDataGridRowFromPointer(e.OriginalSource as DependencyObject);
        }

        private void SyncableDiffGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            SelectDataGridRowFromPointer(e.OriginalSource as DependencyObject);
        }

        private void SyncableDiffGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (sender is not DataGrid dataGrid || !CanOpenSyncContextMenu(dataGrid.SelectedItem))
            {
                e.Handled = true;
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
                var editorText = DiffRenderingService.ReadEditorText(rtbResultConfigText, GetIsPlaceholderParagraph);
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

        private void ComparisonEditor_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not RichTextBox editor)
            {
                return;
            }

            var position = editor.GetPositionFromPoint(e.GetPosition(editor), true);
            if (position?.Paragraph is Paragraph paragraph && GetIsPlaceholderParagraph(paragraph))
            {
                e.Handled = true;
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
                editor.Document = DiffRenderingService.BuildDiffDocument(lines, diffBrush, p => SetIsPlaceholderParagraph(p, true));
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
            var sourceEditorState = applySource switch
            {
                CurrentDifferenceApplySource.Old => synchronizedState.OldEditor,
                CurrentDifferenceApplySource.New => synchronizedState.NewEditor,
                _ => throw new InvalidOperationException("Unknown difference apply source.")
            };

            if (!resultLineIndex.HasValue ||
                resultLineIndex.Value < 0 ||
                resultLineIndex.Value >= synchronizedState.ResultEditor.Lines.Count ||
                resultLineIndex.Value >= sourceEditorState.Lines.Count ||
                !TryUpdateResultTextAtVisualLineIndex(
                    ViewModel.ResultConfigText,
                    synchronizedState.ResultEditor,
                    resultLineIndex.Value,
                    sourceEditorState.Lines[resultLineIndex.Value],
                    out var updatedResultText))
            {
                RefreshDifferenceNavigationButtons();
                return;
            }

            var preferredCaretLineIndex = resultLineIndex.Value;

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

        private static bool TryUpdateResultTextAtVisualLineIndex(
            string resultText,
            TextDiffSynchronizedEditorState resultEditorState,
            int visualLineIndex,
            TextDiffHighlightLine sourceLine,
            out string updatedResultText)
        {
            updatedResultText = resultText;

            if (visualLineIndex < 0 ||
                visualLineIndex >= resultEditorState.ActualLineIndexByVisualLineIndex.Count ||
                visualLineIndex >= resultEditorState.InsertionLineIndexByVisualLineIndex.Count)
            {
                return false;
            }

            var lines = SplitTextLinesForEditing(resultText);
            var targetActualLineIndex = resultEditorState.ActualLineIndexByVisualLineIndex[visualLineIndex];
            var insertionLineIndex = resultEditorState.InsertionLineIndexByVisualLineIndex[visualLineIndex];

            if (sourceLine.IsPlaceholder)
            {
                if (!targetActualLineIndex.HasValue ||
                    targetActualLineIndex.Value < 0 ||
                    targetActualLineIndex.Value >= lines.Count)
                {
                    return false;
                }

                lines.RemoveAt(targetActualLineIndex.Value);
            }
            else if (targetActualLineIndex.HasValue)
            {
                if (targetActualLineIndex.Value < 0 || targetActualLineIndex.Value >= lines.Count)
                {
                    return false;
                }

                lines[targetActualLineIndex.Value] = sourceLine.Text;
            }
            else
            {
                if (insertionLineIndex < 0 || insertionLineIndex > lines.Count)
                {
                    return false;
                }

                lines.Insert(insertionLineIndex, sourceLine.Text);
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

        // BuildDiffDocument, BuildParagraph, ReadEditorText moved to DiffRenderingService

        private static bool GetIsPlaceholderParagraph(DependencyObject element)
        {
            return (bool)element.GetValue(IsPlaceholderParagraphProperty);
        }

        private static void SetIsPlaceholderParagraph(DependencyObject element, bool value)
        {
            element.SetValue(IsPlaceholderParagraphProperty, value);
        }

        private static ScrollViewer? GetScrollViewer(RichTextBox editor)
        {
            return FindDescendant<ScrollViewer>(editor);
        }

        // CreateFrozenBrush moved to DiffRenderingService

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

        private static void SelectDataGridRowFromPointer(DependencyObject? originalSource)
        {
            var row = FindAncestor<DataGridRow>(originalSource);
            if (row?.Item != null)
            {
                row.IsSelected = true;
            }
        }

        private static bool CanOpenSyncContextMenu(object? selectedItem)
        {
            return selectedItem switch
            {
                VersionSyncToggleDiffItem toggleItem => toggleItem.CanSyncPreview,
                VersionSyncParameterDiffItem parameterItem => parameterItem.CanSyncPreview,
                VersionSyncVisibilityDiffItem visibilityItem => visibilityItem.CanSyncPreview,
                _ => false
            };
        }

        private readonly record struct ComparisonScrollPosition(double HorizontalOffset, double VerticalOffset);
    }
}