using System;
using System.Collections.Generic;

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
            var newEditorLines = new List<TextDiffHighlightLine>();
            var newDifferenceLineIndices = new List<int>();
            var newDifferenceOrdinalByLineIndex = new Dictionary<int, int>();
            var resultEditorLines = new List<TextDiffHighlightLine>();
            var resultDifferenceLineIndices = new List<int>();
            var resultDifferenceOrdinalByLineIndex = new Dictionary<int, int>();

            var oldLineIndex = 0;
            var newLineIndex = 0;
            var resultLineIndex = 0;
            var differenceOrdinal = 0;

            foreach (var row in rows)
            {
                if (row.HasDifference)
                {
                    differenceOrdinal++;
                }

                if (row.OldLine != null)
                {
                    oldEditorLines.Add(BuildSynchronizedLine(
                        row.OldLine.Text,
                        row.NewLine?.Text,
                        row.ResultLine?.Text,
                        row.HasDifference));

                    if (row.HasDifference)
                    {
                        oldDifferenceLineIndices.Add(oldLineIndex);
                        oldDifferenceOrdinalByLineIndex[oldLineIndex] = differenceOrdinal;
                    }

                    oldLineIndex++;
                }

                if (row.NewLine != null)
                {
                    newEditorLines.Add(BuildSynchronizedLine(
                        row.NewLine.Text,
                        row.OldLine?.Text,
                        row.ResultLine?.Text,
                        row.HasDifference));

                    if (row.HasDifference)
                    {
                        newDifferenceLineIndices.Add(newLineIndex);
                        newDifferenceOrdinalByLineIndex[newLineIndex] = differenceOrdinal;
                    }

                    newLineIndex++;
                }

                if (row.ResultLine != null)
                {
                    resultEditorLines.Add(BuildSynchronizedLine(
                        row.ResultLine.Text,
                        row.NewLine?.Text,
                        row.OldLine?.Text,
                        row.HasDifference));

                    if (row.HasDifference)
                    {
                        resultDifferenceLineIndices.Add(resultLineIndex);
                        resultDifferenceOrdinalByLineIndex[resultLineIndex] = differenceOrdinal;
                    }

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
                    DifferenceOrdinalByLineIndex = oldDifferenceOrdinalByLineIndex
                },
                NewEditor = new TextDiffSynchronizedEditorState
                {
                    Lines = newEditorLines,
                    DifferenceLineIndices = newDifferenceLineIndices,
                    DifferenceOrdinalByLineIndex = newDifferenceOrdinalByLineIndex
                },
                ResultEditor = new TextDiffSynchronizedEditorState
                {
                    Lines = resultEditorLines,
                    DifferenceLineIndices = resultDifferenceLineIndices,
                    DifferenceOrdinalByLineIndex = resultDifferenceOrdinalByLineIndex
                }
            };
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
            string currentText,
            string? firstOtherText,
            string? secondOtherText,
            bool hasDifference)
        {
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
            var lcsLengths = new int[sourceLines.Count + 1, referenceLines.Count + 1];
            for (var sourceIndex = sourceLines.Count - 1; sourceIndex >= 0; sourceIndex--)
            {
                for (var referenceIndex = referenceLines.Count - 1; referenceIndex >= 0; referenceIndex--)
                {
                    lcsLengths[sourceIndex, referenceIndex] = string.Equals(sourceLines[sourceIndex], referenceLines[referenceIndex], StringComparison.Ordinal)
                        ? lcsLengths[sourceIndex + 1, referenceIndex + 1] + 1
                        : Math.Max(lcsLengths[sourceIndex + 1, referenceIndex], lcsLengths[sourceIndex, referenceIndex + 1]);
                }
            }

            var result = new List<AlignedLinePair>();
            var sourcePointer = 0;
            var referencePointer = 0;

            while (sourcePointer < sourceLines.Count || referencePointer < referenceLines.Count)
            {
                if (sourcePointer < sourceLines.Count &&
                    referencePointer < referenceLines.Count &&
                    string.Equals(sourceLines[sourcePointer], referenceLines[referencePointer], StringComparison.Ordinal))
                {
                    result.Add(new AlignedLinePair(sourceLines[sourcePointer], referenceLines[referencePointer]));
                    sourcePointer++;
                    referencePointer++;
                    continue;
                }

                if (sourcePointer < sourceLines.Count &&
                    referencePointer < referenceLines.Count &&
                    lcsLengths[sourcePointer + 1, referencePointer] == lcsLengths[sourcePointer, referencePointer + 1])
                {
                    var remainingSourceCount = sourceLines.Count - sourcePointer;
                    var remainingReferenceCount = referenceLines.Count - referencePointer;

                    if (remainingReferenceCount > remainingSourceCount)
                    {
                        result.Add(new AlignedLinePair(null, referenceLines[referencePointer]));
                        referencePointer++;
                        continue;
                    }

                    if (remainingSourceCount > remainingReferenceCount)
                    {
                        result.Add(new AlignedLinePair(sourceLines[sourcePointer], null));
                        sourcePointer++;
                        continue;
                    }

                    result.Add(new AlignedLinePair(sourceLines[sourcePointer], referenceLines[referencePointer]));
                    sourcePointer++;
                    referencePointer++;
                    continue;
                }

                if (referencePointer >= referenceLines.Count ||
                    (sourcePointer < sourceLines.Count && lcsLengths[sourcePointer + 1, referencePointer] >= lcsLengths[sourcePointer, referencePointer + 1]))
                {
                    result.Add(new AlignedLinePair(sourceLines[sourcePointer], null));
                    sourcePointer++;
                    continue;
                }

                result.Add(new AlignedLinePair(null, referenceLines[referencePointer]));
                referencePointer++;
            }

            return result;
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

        private sealed record IndexedLine(int LineIndex, string Text);
        private sealed record AlignedLinePair(string? SourceLine, string? ReferenceLine);
        private sealed record SynchronizedDiffRow(
            IndexedLine? OldLine,
            IndexedLine? NewLine,
            IndexedLine? ResultLine,
            bool HasDifference);
    }
}