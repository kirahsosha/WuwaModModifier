using System;
using System.Collections.Generic;
using System.Linq;

namespace WuwaModModifier.Common
{
    public sealed class TextDiffHighlightSpan
    {
        public string Text { get; init; } = string.Empty;
        public bool IsDifferent { get; init; }
    }

    public sealed class TextDiffHighlightLine
    {
        public string Text { get; init; } = string.Empty;
        public IReadOnlyList<TextDiffHighlightSpan> Spans { get; init; } = Array.Empty<TextDiffHighlightSpan>();
        public bool HasDifference { get; init; }
        public bool IsPlaceholder { get; init; }
    }

    public sealed class TextDiffNavigationState
    {
        public int TotalDifferenceCount { get; init; }
        public IReadOnlyList<int> SourceDifferenceLineIndices { get; init; } = Array.Empty<int>();
        public IReadOnlyDictionary<int, int> SourceDifferenceOrdinalByLineIndex { get; init; } = new Dictionary<int, int>();
        public IReadOnlyList<int> ReferenceDifferenceLineIndices { get; init; } = Array.Empty<int>();
        public IReadOnlyDictionary<int, int> ReferenceDifferenceOrdinalByLineIndex { get; init; } = new Dictionary<int, int>();
    }

    public sealed class TextDiffSynchronizedEditorState
    {
        public IReadOnlyList<TextDiffHighlightLine> Lines { get; init; } = Array.Empty<TextDiffHighlightLine>();
        public IReadOnlyList<int> DifferenceLineIndices { get; init; } = Array.Empty<int>();
        public IReadOnlyDictionary<int, int> DifferenceOrdinalByLineIndex { get; init; } = new Dictionary<int, int>();
        public IReadOnlyList<int?> ActualLineIndexByVisualLineIndex { get; init; } = Array.Empty<int?>();
        public IReadOnlyList<int> InsertionLineIndexByVisualLineIndex { get; init; } = Array.Empty<int>();
    }

    public sealed class TextDiffSynchronizedState
    {
        public int TotalDifferenceCount { get; init; }
        public TextDiffSynchronizedEditorState OldEditor { get; init; } = new TextDiffSynchronizedEditorState();
        public TextDiffSynchronizedEditorState NewEditor { get; init; } = new TextDiffSynchronizedEditorState();
        public TextDiffSynchronizedEditorState ResultEditor { get; init; } = new TextDiffSynchronizedEditorState();
    }

    public static class TextDiffHighlighter
    {
        public static IReadOnlyList<TextDiffHighlightLine> BuildHighlightedLines(string sourceText, string referenceText)
        {
            var sourceLines = SplitLines(sourceText);
            var referenceLines = SplitLines(referenceText);
            var alignedPairs = BuildAlignedLinePairs(sourceLines, referenceLines);
            var result = new List<TextDiffHighlightLine>();

            foreach (var alignedPair in alignedPairs)
            {
                if (alignedPair.SourceLine == null)
                {
                    continue;
                }

                result.Add(BuildLine(alignedPair.SourceLine, alignedPair.ReferenceLine));
            }

            return result;
        }

        public static TextDiffNavigationState BuildNavigationState(string sourceText, string referenceText)
        {
            var sourceLines = SplitLines(sourceText);
            var referenceLines = SplitLines(referenceText);
            var alignedPairs = BuildAlignedLinePairs(sourceLines, referenceLines);
            var sourceDifferenceLineIndices = new List<int>();
            var sourceDifferenceOrdinalByLineIndex = new Dictionary<int, int>();
            var referenceDifferenceLineIndices = new List<int>();
            var referenceDifferenceOrdinalByLineIndex = new Dictionary<int, int>();
            var sourceLineIndex = 0;
            var referenceLineIndex = 0;
            var differenceOrdinal = 0;

            foreach (var alignedPair in alignedPairs)
            {
                var hasDifference = alignedPair.SourceLine == null ||
                    alignedPair.ReferenceLine == null ||
                    !string.Equals(alignedPair.SourceLine, alignedPair.ReferenceLine, StringComparison.Ordinal);

                if (hasDifference)
                {
                    differenceOrdinal++;

                    if (alignedPair.SourceLine != null)
                    {
                        sourceDifferenceLineIndices.Add(sourceLineIndex);
                        sourceDifferenceOrdinalByLineIndex[sourceLineIndex] = differenceOrdinal;
                    }

                    if (alignedPair.ReferenceLine != null)
                    {
                        referenceDifferenceLineIndices.Add(referenceLineIndex);
                        referenceDifferenceOrdinalByLineIndex[referenceLineIndex] = differenceOrdinal;
                    }
                }

                if (alignedPair.SourceLine != null)
                {
                    sourceLineIndex++;
                }

                if (alignedPair.ReferenceLine != null)
                {
                    referenceLineIndex++;
                }
            }

            return new TextDiffNavigationState
            {
                TotalDifferenceCount = differenceOrdinal,
                SourceDifferenceLineIndices = sourceDifferenceLineIndices,
                SourceDifferenceOrdinalByLineIndex = sourceDifferenceOrdinalByLineIndex,
                ReferenceDifferenceLineIndices = referenceDifferenceLineIndices,
                ReferenceDifferenceOrdinalByLineIndex = referenceDifferenceOrdinalByLineIndex
            };
        }

        public static TextDiffSynchronizedState BuildSynchronizedState(string oldText, string newText, string resultText)
        {
            var oldLines = SplitLines(oldText);
            var newLines = SplitLines(newText);
            var resultLines = SplitLines(resultText);
            var oldSnapshot = BuildAlignmentSnapshot(oldLines, newLines);
            var resultSnapshot = BuildAlignmentSnapshot(resultLines, newLines);
            var rows = BuildSynchronizedRows(newLines, oldSnapshot, resultSnapshot);

            var oldEditorLines = new List<TextDiffHighlightLine>();
            var oldDifferenceLineIndices = new List<int>();
            var oldDifferenceOrdinalByLineIndex = new Dictionary<int, int>();
            var oldActualLineIndices = new List<int?>();
            var oldInsertionLineIndices = new List<int>();
            var newEditorLines = new List<TextDiffHighlightLine>();
            var newDifferenceLineIndices = new List<int>();
            var newDifferenceOrdinalByLineIndex = new Dictionary<int, int>();
            var newActualLineIndices = new List<int?>();
            var newInsertionLineIndices = new List<int>();
            var resultEditorLines = new List<TextDiffHighlightLine>();
            var resultDifferenceLineIndices = new List<int>();
            var resultDifferenceOrdinalByLineIndex = new Dictionary<int, int>();
            var resultActualLineIndices = new List<int?>();
            var resultInsertionLineIndices = new List<int>();

            var oldLineIndex = 0;
            var newLineIndex = 0;
            var resultLineIndex = 0;
            var differenceOrdinal = 0;
            var previousVisibleDifferenceLineIndex = -1;

            foreach (var row in rows)
            {
                var visualLineIndex = oldEditorLines.Count;
                var hasNavigableDifference = row.HasDifference;

                if (hasNavigableDifference)
                {
                    var currentResultLineText = GetRepresentativeLineText(row);
                    if (!ShouldContinueVisibleDifferenceGroup(
                            previousVisibleDifferenceLineIndex,
                            visualLineIndex,
                            currentResultLineText))
                    {
                        differenceOrdinal++;
                    }

                    previousVisibleDifferenceLineIndex = visualLineIndex;
                }

                oldEditorLines.Add(BuildSynchronizedLine(
                    row.OldLine,
                    row.NewLine?.Text,
                    row.ResultLine?.Text,
                    row.HasDifference));
                oldActualLineIndices.Add(row.OldLine?.LineIndex);
                oldInsertionLineIndices.Add(oldLineIndex);

                if (hasNavigableDifference)
                {
                    oldDifferenceLineIndices.Add(visualLineIndex);
                    oldDifferenceOrdinalByLineIndex[visualLineIndex] = differenceOrdinal;
                }

                if (row.OldLine != null)
                {
                    oldLineIndex++;
                }

                newEditorLines.Add(BuildSynchronizedLine(
                    row.NewLine,
                    row.OldLine?.Text,
                    row.ResultLine?.Text,
                    row.HasDifference));
                newActualLineIndices.Add(row.NewLine?.LineIndex);
                newInsertionLineIndices.Add(newLineIndex);

                if (hasNavigableDifference)
                {
                    newDifferenceLineIndices.Add(visualLineIndex);
                    newDifferenceOrdinalByLineIndex[visualLineIndex] = differenceOrdinal;
                }

                if (row.NewLine != null)
                {
                    newLineIndex++;
                }

                resultEditorLines.Add(BuildSynchronizedLine(
                    row.ResultLine,
                    row.NewLine?.Text,
                    row.OldLine?.Text,
                    row.HasDifference));
                resultActualLineIndices.Add(row.ResultLine?.LineIndex);
                resultInsertionLineIndices.Add(resultLineIndex);

                if (hasNavigableDifference)
                {
                    resultDifferenceLineIndices.Add(visualLineIndex);
                    resultDifferenceOrdinalByLineIndex[visualLineIndex] = differenceOrdinal;
                }

                if (row.ResultLine != null)
                {
                    resultLineIndex++;
                }
            }

            return new TextDiffSynchronizedState
            {
                TotalDifferenceCount = differenceOrdinal,
                OldEditor = new TextDiffSynchronizedEditorState
                {
                    Lines = oldEditorLines,
                    DifferenceLineIndices = oldDifferenceLineIndices,
                    DifferenceOrdinalByLineIndex = oldDifferenceOrdinalByLineIndex,
                    ActualLineIndexByVisualLineIndex = oldActualLineIndices,
                    InsertionLineIndexByVisualLineIndex = oldInsertionLineIndices
                },
                NewEditor = new TextDiffSynchronizedEditorState
                {
                    Lines = newEditorLines,
                    DifferenceLineIndices = newDifferenceLineIndices,
                    DifferenceOrdinalByLineIndex = newDifferenceOrdinalByLineIndex,
                    ActualLineIndexByVisualLineIndex = newActualLineIndices,
                    InsertionLineIndexByVisualLineIndex = newInsertionLineIndices
                },
                ResultEditor = new TextDiffSynchronizedEditorState
                {
                    Lines = resultEditorLines,
                    DifferenceLineIndices = resultDifferenceLineIndices,
                    DifferenceOrdinalByLineIndex = resultDifferenceOrdinalByLineIndex,
                    ActualLineIndexByVisualLineIndex = resultActualLineIndices,
                    InsertionLineIndexByVisualLineIndex = resultInsertionLineIndices
                }
            };
        }

        private static bool ShouldContinueVisibleDifferenceGroup(
            int previousVisibleDifferenceResultLineIndex,
            int currentResultLineIndex,
            string currentResultLineText)
        {
            return previousVisibleDifferenceResultLineIndex >= 0 &&
                currentResultLineIndex == previousVisibleDifferenceResultLineIndex + 1 &&
                !StartsNewStandaloneDifferenceItem(currentResultLineText);
        }

        private static bool StartsNewStandaloneDifferenceItem(string lineText)
        {
            if (string.IsNullOrWhiteSpace(lineText))
            {
                return false;
            }

            var trimmedLineText = lineText.TrimStart();
            if (trimmedLineText.Length != lineText.Length)
            {
                return false;
            }

            return !StartsWithBlockContinuationKeyword(trimmedLineText);
        }

        private static bool StartsWithBlockContinuationKeyword(string lineText)
        {
            return StartsWithKeyword(lineText, "else") ||
                StartsWithKeyword(lineText, "elseif") ||
                StartsWithKeyword(lineText, "endif");
        }

        private static bool StartsWithKeyword(string lineText, string keyword)
        {
            if (!lineText.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return lineText.Length == keyword.Length ||
                char.IsWhiteSpace(lineText[keyword.Length]);
        }

        public static IReadOnlyDictionary<int, string?> BuildSourceLineTextByReferenceLineIndexForReplacement(string sourceText, string referenceText)
        {
            var sourceLines = SplitLines(sourceText);
            var referenceLines = SplitLines(referenceText);
            var alignedPairs = BuildAlignedLinePairsForReplacement(sourceLines, referenceLines);
            var sourceLineTextByReferenceLineIndex = new Dictionary<int, string?>();
            var referenceLineIndex = 0;

            foreach (var alignedPair in alignedPairs)
            {
                if (alignedPair.ReferenceLine == null)
                {
                    continue;
                }

                sourceLineTextByReferenceLineIndex[referenceLineIndex] = alignedPair.SourceLine;
                referenceLineIndex++;
            }

            return sourceLineTextByReferenceLineIndex;
        }

        public static IReadOnlyList<int> GetDifferenceLineIndices(IReadOnlyList<TextDiffHighlightLine> lines)
        {
            if (lines == null)
            {
                throw new ArgumentNullException(nameof(lines));
            }

            var result = new List<int>();
            for (var index = 0; index < lines.Count; index++)
            {
                if (lines[index].HasDifference)
                {
                    result.Add(index);
                }
            }

            return result;
        }

        public static int? FindPreviousDifferenceLineIndex(IReadOnlyList<int> differenceLineIndices, int currentLineIndex)
        {
            if (differenceLineIndices == null)
            {
                throw new ArgumentNullException(nameof(differenceLineIndices));
            }

            for (var index = differenceLineIndices.Count - 1; index >= 0; index--)
            {
                if (differenceLineIndices[index] < currentLineIndex)
                {
                    return differenceLineIndices[index];
                }
            }

            return null;
        }

        public static int? FindNextDifferenceLineIndex(IReadOnlyList<int> differenceLineIndices, int currentLineIndex)
        {
            if (differenceLineIndices == null)
            {
                throw new ArgumentNullException(nameof(differenceLineIndices));
            }

            for (var index = 0; index < differenceLineIndices.Count; index++)
            {
                if (differenceLineIndices[index] > currentLineIndex)
                {
                    return differenceLineIndices[index];
                }
            }

            return null;
        }

        public static int? FindDifferenceOrdinal(IReadOnlyList<int> differenceLineIndices, int currentLineIndex)
        {
            if (differenceLineIndices == null)
            {
                throw new ArgumentNullException(nameof(differenceLineIndices));
            }

            for (var index = 0; index < differenceLineIndices.Count; index++)
            {
                if (differenceLineIndices[index] == currentLineIndex)
                {
                    return index + 1;
                }
            }

            return null;
        }

        public static int? FindNavigationTargetOrdinal(
            IReadOnlyList<int> differenceLineIndices,
            IReadOnlyDictionary<int, int> differenceOrdinalByLineIndex,
            int currentLineIndex,
            bool moveNext,
            int totalDifferenceCount)
        {
            if (differenceLineIndices == null)
            {
                throw new ArgumentNullException(nameof(differenceLineIndices));
            }

            if (differenceOrdinalByLineIndex == null)
            {
                throw new ArgumentNullException(nameof(differenceOrdinalByLineIndex));
            }

            if (totalDifferenceCount <= 0)
            {
                return null;
            }

            if (totalDifferenceCount == 1)
            {
                return 1;
            }

            if (differenceOrdinalByLineIndex.TryGetValue(currentLineIndex, out var currentOrdinal))
            {
                return moveNext
                    ? currentOrdinal == totalDifferenceCount ? 1 : currentOrdinal + 1
                    : currentOrdinal == 1 ? totalDifferenceCount : currentOrdinal - 1;
            }

            if (differenceLineIndices.Count == 0)
            {
                return moveNext ? 1 : totalDifferenceCount;
            }

            if (moveNext)
            {
                for (var index = 0; index < differenceLineIndices.Count; index++)
                {
                    var differenceLineIndex = differenceLineIndices[index];
                    if (differenceLineIndex > currentLineIndex &&
                        differenceOrdinalByLineIndex.TryGetValue(differenceLineIndex, out var nextOrdinal))
                    {
                        return nextOrdinal;
                    }
                }

                return differenceOrdinalByLineIndex.TryGetValue(differenceLineIndices[0], out var wrappedNextOrdinal)
                    ? wrappedNextOrdinal
                    : null;
            }

            for (var index = differenceLineIndices.Count - 1; index >= 0; index--)
            {
                var differenceLineIndex = differenceLineIndices[index];
                if (differenceLineIndex < currentLineIndex &&
                    differenceOrdinalByLineIndex.TryGetValue(differenceLineIndex, out var previousOrdinal))
                {
                    return previousOrdinal;
                }
            }

            return differenceOrdinalByLineIndex.TryGetValue(differenceLineIndices[differenceLineIndices.Count - 1], out var wrappedPreviousOrdinal)
                ? wrappedPreviousOrdinal
                : null;
        }

        public static int? FindLineIndexByDifferenceOrdinal(
            IReadOnlyDictionary<int, int> differenceOrdinalByLineIndex,
            int differenceOrdinal)
        {
            if (differenceOrdinalByLineIndex == null)
            {
                throw new ArgumentNullException(nameof(differenceOrdinalByLineIndex));
            }

            foreach (var pair in differenceOrdinalByLineIndex)
            {
                if (pair.Value == differenceOrdinal)
                {
                    return pair.Key;
                }
            }

            return null;
        }

        private static TextDiffHighlightLine BuildSynchronizedLine(
            IndexedLine? currentLine,
            string? firstOtherText,
            string? secondOtherText,
            bool hasDifference)
        {
            if (currentLine == null)
            {
                return BuildPlaceholderLine(hasDifference);
            }

            var currentText = currentLine.Text;
            if (!hasDifference)
            {
                return new TextDiffHighlightLine
                {
                    Text = currentText,
                    Spans = new[]
                    {
                        new TextDiffHighlightSpan
                        {
                            Text = currentText,
                            IsDifferent = false
                        }
                    }
                };
            }

            var comparisonText = SelectComparisonText(currentText, firstOtherText, secondOtherText);
            if (comparisonText == null)
            {
                return new TextDiffHighlightLine
                {
                    Text = currentText,
                    HasDifference = true,
                    Spans = currentText.Length == 0
                        ? Array.Empty<TextDiffHighlightSpan>()
                        : new[]
                        {
                            new TextDiffHighlightSpan
                            {
                                Text = currentText,
                                IsDifferent = true
                            }
                        }
                };
            }

            var line = BuildLine(currentText, comparisonText);
            if (line.HasDifference)
            {
                return line;
            }

            return new TextDiffHighlightLine
            {
                Text = line.Text,
                Spans = line.Spans,
                HasDifference = true
            };
        }

        private static TextDiffHighlightLine BuildPlaceholderLine(bool hasDifference)
        {
            return new TextDiffHighlightLine
            {
                Text = string.Empty,
                Spans = Array.Empty<TextDiffHighlightSpan>(),
                HasDifference = hasDifference,
                IsPlaceholder = true
            };
        }

        private static string GetRepresentativeLineText(SynchronizedDiffRow row)
        {
            return row.ResultLine?.Text ?? row.NewLine?.Text ?? row.OldLine?.Text ?? string.Empty;
        }

        private static string? SelectComparisonText(string currentText, string? firstOtherText, string? secondOtherText)
        {
            if (firstOtherText != null && !string.Equals(currentText, firstOtherText, StringComparison.Ordinal))
            {
                return firstOtherText;
            }

            if (secondOtherText != null && !string.Equals(currentText, secondOtherText, StringComparison.Ordinal))
            {
                return secondOtherText;
            }

            return firstOtherText ?? secondOtherText;
        }

        private static List<SynchronizedDiffRow> BuildSynchronizedRows(
            IReadOnlyList<string> newLines,
            AlignmentSnapshot oldSnapshot,
            AlignmentSnapshot resultSnapshot)
        {
            var rows = new List<SynchronizedDiffRow>();

            for (var referenceLineIndex = 0; referenceLineIndex <= newLines.Count; referenceLineIndex++)
            {
                oldSnapshot.InsertionsBeforeReferenceIndex.TryGetValue(referenceLineIndex, out var oldInsertions);
                resultSnapshot.InsertionsBeforeReferenceIndex.TryGetValue(referenceLineIndex, out var resultInsertions);

                var insertionCount = Math.Max(oldInsertions?.Count ?? 0, resultInsertions?.Count ?? 0);
                for (var insertionIndex = 0; insertionIndex < insertionCount; insertionIndex++)
                {
                    var oldInsertedLine = oldInsertions != null && insertionIndex < oldInsertions.Count
                        ? oldInsertions[insertionIndex]
                        : null;
                    var resultInsertedLine = resultInsertions != null && insertionIndex < resultInsertions.Count
                        ? resultInsertions[insertionIndex]
                        : null;

                    rows.Add(new SynchronizedDiffRow(
                        oldInsertedLine,
                        null,
                        resultInsertedLine,
                        HasSynchronizedDifference(oldInsertedLine, null, resultInsertedLine)));
                }

                if (referenceLineIndex == newLines.Count)
                {
                    break;
                }

                oldSnapshot.SourceLinesByReferenceIndex.TryGetValue(referenceLineIndex, out var oldLine);
                resultSnapshot.SourceLinesByReferenceIndex.TryGetValue(referenceLineIndex, out var resultLine);
                var newLine = new IndexedLine(referenceLineIndex, newLines[referenceLineIndex]);

                rows.Add(new SynchronizedDiffRow(
                    oldLine,
                    newLine,
                    resultLine,
                    HasSynchronizedDifference(oldLine, newLine, resultLine)));
            }

            return rows;
        }

        private static bool HasSynchronizedDifference(IndexedLine? oldLine, IndexedLine? newLine, IndexedLine? resultLine)
        {
            if (oldLine != null && newLine != null && resultLine != null)
            {
                return !string.Equals(oldLine.Text, newLine.Text, StringComparison.Ordinal) ||
                    !string.Equals(oldLine.Text, resultLine.Text, StringComparison.Ordinal) ||
                    !string.Equals(newLine.Text, resultLine.Text, StringComparison.Ordinal);
            }

            return oldLine != null || newLine != null || resultLine != null;
        }

        private static AlignmentSnapshot BuildAlignmentSnapshot(IReadOnlyList<string> sourceLines, IReadOnlyList<string> referenceLines)
        {
            var alignedPairs = BuildAlignedLinePairs(sourceLines, referenceLines);
            var sourceLinesByReferenceIndex = new Dictionary<int, IndexedLine>();
            var insertionsBeforeReferenceIndex = new Dictionary<int, List<IndexedLine>>();
            var sourceLineIndex = 0;
            var referenceLineIndex = 0;

            foreach (var alignedPair in alignedPairs)
            {
                if (alignedPair.SourceLine != null && alignedPair.ReferenceLine == null)
                {
                    if (!insertionsBeforeReferenceIndex.TryGetValue(referenceLineIndex, out var insertedLines))
                    {
                        insertedLines = new List<IndexedLine>();
                        insertionsBeforeReferenceIndex[referenceLineIndex] = insertedLines;
                    }

                    insertedLines.Add(new IndexedLine(sourceLineIndex, alignedPair.SourceLine));
                    sourceLineIndex++;
                    continue;
                }

                if (alignedPair.ReferenceLine != null)
                {
                    if (alignedPair.SourceLine != null)
                    {
                        sourceLinesByReferenceIndex[referenceLineIndex] = new IndexedLine(sourceLineIndex, alignedPair.SourceLine);
                        sourceLineIndex++;
                    }

                    referenceLineIndex++;
                }
            }

            return new AlignmentSnapshot(sourceLinesByReferenceIndex, insertionsBeforeReferenceIndex);
        }

        private static TextDiffHighlightLine BuildLine(string sourceLine, string? referenceLine)
        {
            if (referenceLine == null)
            {
                return new TextDiffHighlightLine
                {
                    Text = sourceLine,
                    HasDifference = true,
                    Spans = new[]
                    {
                        new TextDiffHighlightSpan
                        {
                            Text = sourceLine,
                            IsDifferent = true
                        }
                    }
                };
            }

            if (string.Equals(sourceLine, referenceLine, StringComparison.Ordinal))
            {
                return new TextDiffHighlightLine
                {
                    Text = sourceLine,
                    Spans = new[]
                    {
                        new TextDiffHighlightSpan
                        {
                            Text = sourceLine,
                            IsDifferent = false
                        }
                    }
                };
            }

            var sharedPrefixLength = GetSharedPrefixLength(sourceLine, referenceLine);
            var sharedSuffixLength = GetSharedSuffixLength(sourceLine, referenceLine, sharedPrefixLength);
            var spans = new List<TextDiffHighlightSpan>();

            if (sharedPrefixLength > 0)
            {
                spans.Add(new TextDiffHighlightSpan
                {
                    Text = sourceLine[..sharedPrefixLength],
                    IsDifferent = false
                });
            }

            var differingLength = sourceLine.Length - sharedPrefixLength - sharedSuffixLength;
            if (differingLength > 0)
            {
                spans.Add(new TextDiffHighlightSpan
                {
                    Text = sourceLine.Substring(sharedPrefixLength, differingLength),
                    IsDifferent = true
                });
            }

            if (sharedSuffixLength > 0)
            {
                spans.Add(new TextDiffHighlightSpan
                {
                    Text = sourceLine[^sharedSuffixLength..],
                    IsDifferent = false
                });
            }

            return new TextDiffHighlightLine
            {
                Text = sourceLine,
                HasDifference = true,
                Spans = spans
            };
        }

        private static List<AlignedLinePair> BuildAlignedLinePairs(IReadOnlyList<string> sourceLines, IReadOnlyList<string> referenceLines)
        {
            return BuildAlignedLinePairsCore(sourceLines, referenceLines, useReplacementTieBreak: false);
        }

        private static List<AlignedLinePair> BuildAlignedLinePairsForReplacement(IReadOnlyList<string> sourceLines, IReadOnlyList<string> referenceLines)
        {
            return BuildAlignedLinePairsCore(sourceLines, referenceLines, useReplacementTieBreak: true);
        }

        /// <summary>
        /// Shared LCS-based alignment core.  When <paramref name="useReplacementTieBreak"/>
        /// is <c>true</c> the tie-breaking favours look-ahead equivalence checks (used by
        /// the replacement path); otherwise it falls back on remaining-line-count heuristics.
        /// </summary>
        private static List<AlignedLinePair> BuildAlignedLinePairsCore(
            IReadOnlyList<string> sourceLines,
            IReadOnlyList<string> referenceLines,
            bool useReplacementTieBreak)
        {
            var sourceEntries = BuildAlignmentEntries(sourceLines);
            var referenceEntries = BuildAlignmentEntries(referenceLines);

            // ── DP table (standard LCS) ──
            var lcsLengths = new int[sourceEntries.Count + 1, referenceEntries.Count + 1];
            for (var si = sourceEntries.Count - 1; si >= 0; si--)
            {
                for (var ri = referenceEntries.Count - 1; ri >= 0; ri--)
                {
                    lcsLengths[si, ri] = AreLinesEquivalentForAlignment(sourceEntries[si], referenceEntries[ri])
                        ? lcsLengths[si + 1, ri + 1] + 1
                        : Math.Max(lcsLengths[si + 1, ri], lcsLengths[si, ri + 1]);
                }
            }

            // ── Backtrack ──
            var result = new List<AlignedLinePair>();
            var sp = 0;
            var rp = 0;

            while (sp < sourceEntries.Count || rp < referenceEntries.Count)
            {
                // Direct match
                if (sp < sourceEntries.Count && rp < referenceEntries.Count &&
                    AreLinesEquivalentForAlignment(sourceEntries[sp], referenceEntries[rp]))
                {
                    result.Add(new AlignedLinePair(sourceEntries[sp].Text, referenceEntries[rp].Text));
                    sp++;
                    rp++;
                    continue;
                }

                // Equal LCS score → resolve the tie
                if (sp < sourceEntries.Count && rp < referenceEntries.Count &&
                    lcsLengths[sp + 1, rp] == lcsLengths[sp, rp + 1])
                {
                    if (useReplacementTieBreak)
                    {
                        if (rp + 1 < referenceEntries.Count &&
                            AreLinesEquivalentForAlignment(sourceEntries[sp], referenceEntries[rp + 1]))
                        {
                            result.Add(new AlignedLinePair(null, referenceEntries[rp].Text));
                            rp++;
                            continue;
                        }

                        if (sp + 1 < sourceEntries.Count &&
                            AreLinesEquivalentForAlignment(sourceEntries[sp + 1], referenceEntries[rp]))
                        {
                            result.Add(new AlignedLinePair(sourceEntries[sp].Text, null));
                            sp++;
                            continue;
                        }

                        result.Add(new AlignedLinePair(sourceEntries[sp].Text, referenceEntries[rp].Text));
                        sp++;
                        rp++;
                        continue;
                    }

                    var remainingSource = sourceEntries.Count - sp;
                    var remainingRef = referenceEntries.Count - rp;

                    if (remainingRef > remainingSource)
                    {
                        result.Add(new AlignedLinePair(null, referenceEntries[rp].Text));
                        rp++;
                        continue;
                    }

                    if (remainingSource > remainingRef)
                    {
                        result.Add(new AlignedLinePair(sourceEntries[sp].Text, null));
                        sp++;
                        continue;
                    }

                    result.Add(new AlignedLinePair(sourceEntries[sp].Text, referenceEntries[rp].Text));
                    sp++;
                    rp++;
                    continue;
                }

                // Delete source line
                if (rp >= referenceEntries.Count ||
                    (sp < sourceEntries.Count && lcsLengths[sp + 1, rp] >= lcsLengths[sp, rp + 1]))
                {
                    result.Add(new AlignedLinePair(sourceEntries[sp].Text, null));
                    sp++;
                    continue;
                }

                // Insert reference line
                result.Add(new AlignedLinePair(null, referenceEntries[rp].Text));
                rp++;
            }

            return result;
        }

        private static bool AreLinesEquivalentForAlignment(AlignmentLine leftLine, AlignmentLine rightLine)
        {
            if (string.Equals(leftLine.Text, rightLine.Text, StringComparison.Ordinal))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(leftLine.AlignmentKey) &&
                !string.IsNullOrWhiteSpace(rightLine.AlignmentKey) &&
                string.Equals(leftLine.AlignmentKey, rightLine.AlignmentKey, StringComparison.OrdinalIgnoreCase);
        }

        private static List<AlignmentLine> BuildAlignmentEntries(IReadOnlyList<string> lines)
        {
            var result = new List<AlignmentLine>(lines.Count);
            string? pendingDrawComponentId = null;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                string? alignmentKey = null;

                if (TryGetGlobalAlignmentKey(trimmedLine, out var globalAlignmentKey))
                {
                    alignmentKey = globalAlignmentKey;
                    pendingDrawComponentId = null;
                }
                else if (TryGetDrawComponentId(trimmedLine, out var drawComponentId))
                {
                    alignmentKey = $"draw-comment:{drawComponentId}";
                    pendingDrawComponentId = drawComponentId;
                }
                else if (TryGetDrawCommandName(trimmedLine, out var drawCommandName))
                {
                    alignmentKey = pendingDrawComponentId == null
                        ? $"{drawCommandName}"
                        : $"{drawCommandName}:{pendingDrawComponentId}";
                }
                else if (!string.IsNullOrWhiteSpace(trimmedLine))
                {
                    pendingDrawComponentId = null;
                }

                result.Add(new AlignmentLine(line, alignmentKey));
            }

            return result;
        }

        private static bool TryGetGlobalAlignmentKey(string trimmedLine, out string key)
        {
            key = string.Empty;
            if (string.IsNullOrWhiteSpace(trimmedLine))
            {
                return false;
            }

            if (!trimmedLine.StartsWith("global ", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var remainder = trimmedLine.Substring("global ".Length).TrimStart();
            if (remainder.StartsWith("persist ", StringComparison.OrdinalIgnoreCase))
            {
                remainder = remainder.Substring("persist ".Length).TrimStart();
            }

            var equalsIndex = remainder.IndexOf('=');
            if (equalsIndex <= 0)
            {
                return false;
            }

            var variableToken = remainder[..equalsIndex]
                .Trim()
                .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(variableToken) || !variableToken.StartsWith("$", StringComparison.Ordinal))
            {
                return false;
            }

            key = $"global:{variableToken}";
            return true;
        }

        private static bool TryGetDrawComponentId(string trimmedLine, out string componentId)
        {
            componentId = string.Empty;
            const string prefix = "; Draw Component ";
            if (!trimmedLine.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var remainder = trimmedLine.Substring(prefix.Length).TrimStart();
            if (string.IsNullOrWhiteSpace(remainder))
            {
                return false;
            }

            var length = 0;
            while (length < remainder.Length)
            {
                var character = remainder[length];
                if (!IsAsciiAlignmentTokenCharacter(character))
                {
                    break;
                }

                length++;
            }

            if (length == 0)
            {
                return false;
            }

            componentId = remainder[..length];
            return true;
        }

        private static bool IsAsciiAlignmentTokenCharacter(char character)
        {
            return (character >= '0' && character <= '9') ||
                (character >= 'A' && character <= 'Z') ||
                (character >= 'a' && character <= 'z') ||
                character == '.' ||
                character == '_' ||
                character == '-';
        }

        private static bool TryGetDrawCommandName(string trimmedLine, out string drawCommandName)
        {
            drawCommandName = string.Empty;
            const string drawIndexedPrefix = "drawindexed";
            if (!trimmedLine.StartsWith(drawIndexedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (trimmedLine.Length > drawIndexedPrefix.Length)
            {
                var nextCharacter = trimmedLine[drawIndexedPrefix.Length];
                if (!(char.IsWhiteSpace(nextCharacter) || nextCharacter == '='))
                {
                    return false;
                }
            }

            drawCommandName = drawIndexedPrefix;
            return true;
        }

        private static int GetSharedPrefixLength(string sourceLine, string referenceLine)
        {
            var index = 0;
            var maxLength = Math.Min(sourceLine.Length, referenceLine.Length);
            while (index < maxLength && sourceLine[index] == referenceLine[index])
            {
                index++;
            }

            return index;
        }

        private static int GetSharedSuffixLength(string sourceLine, string referenceLine, int prefixLength)
        {
            var sourceIndex = sourceLine.Length - 1;
            var referenceIndex = referenceLine.Length - 1;
            var maxSuffixLength = Math.Min(sourceLine.Length, referenceLine.Length) - prefixLength;
            var sharedLength = 0;

            while (sharedLength < maxSuffixLength && sourceLine[sourceIndex] == referenceLine[referenceIndex])
            {
                sourceIndex--;
                referenceIndex--;
                sharedLength++;
            }

            return sharedLength;
        }

        private static string[] SplitLines(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Array.Empty<string>();
            }

            return text
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n');
        }

        private sealed class AlignmentSnapshot
        {
            public AlignmentSnapshot(
                Dictionary<int, IndexedLine> sourceLinesByReferenceIndex,
                Dictionary<int, List<IndexedLine>> insertionsBeforeReferenceIndex)
            {
                SourceLinesByReferenceIndex = sourceLinesByReferenceIndex;
                InsertionsBeforeReferenceIndex = insertionsBeforeReferenceIndex;
            }

            public Dictionary<int, IndexedLine> SourceLinesByReferenceIndex { get; }

            public Dictionary<int, List<IndexedLine>> InsertionsBeforeReferenceIndex { get; }
        }

        private sealed record AlignmentLine(string Text, string? AlignmentKey);
        private sealed record IndexedLine(int LineIndex, string Text);
        private sealed record AlignedLinePair(string? SourceLine, string? ReferenceLine);
        private sealed record SynchronizedDiffRow(
            IndexedLine? OldLine,
            IndexedLine? NewLine,
            IndexedLine? ResultLine,
            bool HasDifference);
    }
}