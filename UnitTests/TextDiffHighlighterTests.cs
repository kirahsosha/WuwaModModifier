using System.Linq;
using WuwaModModifier.Common;

namespace UnitTests
{
    public class TextDiffHighlighterTests
    {
        [Fact]
        public void BuildHighlightedLines_ShouldKeepUnchangedLinesPlain()
        {
            var lines = TextDiffHighlighter.BuildHighlightedLines("line1\nline2", "line1\nline2");

            Assert.Equal(2, lines.Count);
            Assert.All(lines, line => Assert.False(line.HasDifference));
            Assert.All(lines, line => Assert.All(line.Spans, span => Assert.False(span.IsDifferent)));
        }

        [Fact]
        public void BuildHighlightedLines_ShouldHighlightOnlyChangedMiddleSpan()
        {
            var lines = TextDiffHighlighter.BuildHighlightedLines("key = F3", "key = F8");

            var line = Assert.Single(lines);
            Assert.True(line.HasDifference);
            Assert.Equal(2, line.Spans.Count);
            Assert.Equal("key = F", line.Spans[0].Text);
            Assert.False(line.Spans[0].IsDifferent);
            Assert.Equal("3", line.Spans[1].Text);
            Assert.True(line.Spans[1].IsDifferent);
        }

        [Fact]
        public void BuildHighlightedLines_ShouldAlignAroundInsertedReferenceLines()
        {
            var lines = TextDiffHighlighter.BuildHighlightedLines("line1\nline3", "line1\nline2\nline3");

            Assert.Equal(2, lines.Count);
            Assert.Equal("line1", lines[0].Text);
            Assert.False(lines[0].HasDifference);
            Assert.Equal("line3", lines[1].Text);
            Assert.False(lines[1].HasDifference);
        }

        [Fact]
        public void BuildHighlightedLines_ShouldMarkDeletedSourceLineAsDifferent()
        {
            var lines = TextDiffHighlighter.BuildHighlightedLines("line1\nline2\nline3", "line1\nline3");

            Assert.Equal(3, lines.Count);
            Assert.False(lines[0].HasDifference);
            Assert.True(lines[1].HasDifference);
            Assert.Equal("line2", lines[1].Text);
            Assert.Single(lines[1].Spans);
            Assert.True(lines[1].Spans.Single().IsDifferent);
            Assert.False(lines[2].HasDifference);
        }

        [Fact]
        public void GetDifferenceLineIndices_ShouldReturnOnlyChangedLineIndices()
        {
            var lines = TextDiffHighlighter.BuildHighlightedLines(
                "same\nold\nsame2\nold2",
                "same\nnew\nsame2\nnew2");

            var indices = TextDiffHighlighter.GetDifferenceLineIndices(lines);

            Assert.Equal(new[] { 1, 3 }, indices);
        }

        [Fact]
        public void FindPreviousDifferenceLineIndex_ShouldSkipCurrentDifferenceLine()
        {
            var indices = new[] { 1, 3, 6 };

            var previous = TextDiffHighlighter.FindPreviousDifferenceLineIndex(indices, 3);

            Assert.Equal(1, previous);
        }

        [Fact]
        public void FindNextDifferenceLineIndex_ShouldSkipCurrentDifferenceLine()
        {
            var indices = new[] { 1, 3, 6 };

            var next = TextDiffHighlighter.FindNextDifferenceLineIndex(indices, 3);

            Assert.Equal(6, next);
        }

        [Fact]
        public void FindPreviousAndNextDifferenceLineIndex_ShouldReturnNullAtEdges()
        {
            var indices = new[] { 1, 3, 6 };

            Assert.Null(TextDiffHighlighter.FindPreviousDifferenceLineIndex(indices, 1));
            Assert.Null(TextDiffHighlighter.FindNextDifferenceLineIndex(indices, 6));
            Assert.Equal(1, TextDiffHighlighter.FindNextDifferenceLineIndex(indices, 0));
            Assert.Equal(6, TextDiffHighlighter.FindPreviousDifferenceLineIndex(indices, 7));
        }

        [Fact]
        public void FindDifferenceOrdinal_ShouldReturnOneBasedOrdinalOnlyWhenCurrentLineIsDifference()
        {
            var indices = new[] { 1, 3, 6 };

            Assert.Equal(1, TextDiffHighlighter.FindDifferenceOrdinal(indices, 1));
            Assert.Equal(2, TextDiffHighlighter.FindDifferenceOrdinal(indices, 3));
            Assert.Equal(3, TextDiffHighlighter.FindDifferenceOrdinal(indices, 6));
            Assert.Null(TextDiffHighlighter.FindDifferenceOrdinal(indices, 2));
            Assert.Null(TextDiffHighlighter.FindDifferenceOrdinal(indices, 7));
        }

        [Fact]
        public void BuildNavigationState_ShouldAssignSharedOrdinalsAcrossHiddenInsertedReferenceLines()
        {
            var navigationState = TextDiffHighlighter.BuildNavigationState(
                "same\nold value\nsame2",
                "same\ninserted value\nnew value\nsame2");

            Assert.Equal(2, navigationState.TotalDifferenceCount);
            Assert.Equal(new[] { 1 }, navigationState.SourceDifferenceLineIndices);
            Assert.Equal(2, navigationState.SourceDifferenceOrdinalByLineIndex[1]);
            Assert.Equal(new[] { 1, 2 }, navigationState.ReferenceDifferenceLineIndices);
            Assert.Equal(1, navigationState.ReferenceDifferenceOrdinalByLineIndex[1]);
            Assert.Equal(2, navigationState.ReferenceDifferenceOrdinalByLineIndex[2]);
        }

        [Fact]
        public void BuildNavigationState_ShouldCountDeletedSourceLinesInSharedTotal()
        {
            var navigationState = TextDiffHighlighter.BuildNavigationState(
                "same\ndeleted value\nold value",
                "same\nnew value");

            Assert.Equal(2, navigationState.TotalDifferenceCount);
            Assert.Equal(new[] { 1, 2 }, navigationState.SourceDifferenceLineIndices);
            Assert.Equal(1, navigationState.SourceDifferenceOrdinalByLineIndex[1]);
            Assert.Equal(2, navigationState.SourceDifferenceOrdinalByLineIndex[2]);
            Assert.Equal(new[] { 1 }, navigationState.ReferenceDifferenceLineIndices);
            Assert.Equal(2, navigationState.ReferenceDifferenceOrdinalByLineIndex[1]);
        }

        [Fact]
        public void BuildSynchronizedState_ShouldHighlightAllThreeEditorsWhenAnyTwoLinesDiffer()
        {
            var synchronizedState = TextDiffHighlighter.BuildSynchronizedState(
                "same\nold-value",
                "same\nnew-value",
                "same\nold-value");

            Assert.Equal(1, synchronizedState.TotalDifferenceCount);
            Assert.Equal(new[] { 1 }, synchronizedState.OldEditor.DifferenceLineIndices);
            Assert.Equal(new[] { 1 }, synchronizedState.NewEditor.DifferenceLineIndices);
            Assert.Equal(new[] { 1 }, synchronizedState.ResultEditor.DifferenceLineIndices);
            Assert.True(synchronizedState.OldEditor.Lines[1].HasDifference);
            Assert.True(synchronizedState.NewEditor.Lines[1].HasDifference);
            Assert.True(synchronizedState.ResultEditor.Lines[1].HasDifference);
        }

        [Fact]
        public void BuildSynchronizedState_ShouldAssignSameOrdinalAcrossThreeEditors()
        {
            var synchronizedState = TextDiffHighlighter.BuildSynchronizedState(
                "line1\nold-a\nline3\nold-b",
                "line1\nnew-a\nline3\nnew-b",
                "line1\nresult-a\nline3\nresult-b");

            Assert.Equal(2, synchronizedState.TotalDifferenceCount);
            Assert.Equal(1, synchronizedState.OldEditor.DifferenceOrdinalByLineIndex[1]);
            Assert.Equal(1, synchronizedState.NewEditor.DifferenceOrdinalByLineIndex[1]);
            Assert.Equal(1, synchronizedState.ResultEditor.DifferenceOrdinalByLineIndex[1]);
            Assert.Equal(2, synchronizedState.OldEditor.DifferenceOrdinalByLineIndex[3]);
            Assert.Equal(2, synchronizedState.NewEditor.DifferenceOrdinalByLineIndex[3]);
            Assert.Equal(2, synchronizedState.ResultEditor.DifferenceOrdinalByLineIndex[3]);
        }

        [Fact]
        public void BuildSynchronizedState_ShouldUseDenseOrdinalsForResultVisibleDifferences()
        {
            var synchronizedState = TextDiffHighlighter.BuildSynchronizedState(
                "same\nold-only-before\nold-a\nmiddle\nold-b",
                "same\nnew-a\nmiddle\nnew-b",
                "same\nresult-a\nmiddle\nresult-b");

            Assert.Equal(3, synchronizedState.TotalDifferenceCount);

            Assert.True(synchronizedState.OldEditor.Lines[1].HasDifference);
            Assert.Equal(new[] { 1, 2, 4 }, synchronizedState.OldEditor.DifferenceLineIndices);
            Assert.Equal(1, synchronizedState.OldEditor.DifferenceOrdinalByLineIndex[1]);
            Assert.Equal(2, synchronizedState.OldEditor.DifferenceOrdinalByLineIndex[2]);
            Assert.Equal(3, synchronizedState.OldEditor.DifferenceOrdinalByLineIndex[4]);

            Assert.Equal(new[] { 1, 2, 4 }, synchronizedState.NewEditor.DifferenceLineIndices);
            Assert.Equal(1, synchronizedState.NewEditor.DifferenceOrdinalByLineIndex[1]);
            Assert.Equal(2, synchronizedState.NewEditor.DifferenceOrdinalByLineIndex[2]);
            Assert.Equal(3, synchronizedState.NewEditor.DifferenceOrdinalByLineIndex[4]);

            Assert.Equal(new[] { 1, 2, 4 }, synchronizedState.ResultEditor.DifferenceLineIndices);
            Assert.Equal(1, synchronizedState.ResultEditor.DifferenceOrdinalByLineIndex[1]);
            Assert.Equal(2, synchronizedState.ResultEditor.DifferenceOrdinalByLineIndex[2]);
            Assert.Equal(3, synchronizedState.ResultEditor.DifferenceOrdinalByLineIndex[4]);

            Assert.Equal(
                2,
                TextDiffHighlighter.FindNavigationTargetOrdinal(
                    synchronizedState.ResultEditor.DifferenceLineIndices,
                    synchronizedState.ResultEditor.DifferenceOrdinalByLineIndex,
                    1,
                    moveNext: true,
                    synchronizedState.TotalDifferenceCount));
            Assert.Equal(
                2,
                TextDiffHighlighter.FindLineIndexByDifferenceOrdinal(
                    synchronizedState.ResultEditor.DifferenceOrdinalByLineIndex,
                    2));
        }

        [Fact]
        public void BuildSynchronizedState_ShouldTreatContinuousIndentedBlockAsSingleDifference()
        {
            var synchronizedState = TextDiffHighlighter.BuildSynchronizedState(
                "\tif $mod_enabled\n\t\tpost $object_detected = 0\n\t\trun = CommandListUpdateMergedSkeleton\n\telse\n\t\tif $mod_id == -1000\n\t\t\trun = CommandListRegisterMod\n\t\tendif\n\tendif",
                "    if $mod_enabled\n        post $object_detected = 0\n        run = CommandListUpdateMergedSkeleton\n    else\n        if $mod_id == -1000\n            run = CommandListRegisterMod\n        endif\n    endif",
                "\tif $mod_enabled\n\t\tpost $object_detected = 0\n\t\trun = CommandListUpdateMergedSkeleton\n\telse\n\t\tif $mod_id == -1000\n\t\t\trun = CommandListRegisterMod\n\t\tendif\n\tendif");

            Assert.Equal(1, synchronizedState.TotalDifferenceCount);
            Assert.All(
                synchronizedState.ResultEditor.DifferenceLineIndices,
                lineIndex => Assert.Equal(1, synchronizedState.ResultEditor.DifferenceOrdinalByLineIndex[lineIndex]));
        }

        [Fact]
        public void BuildSynchronizedState_ShouldKeepConsecutiveTopLevelAssignmentsAsSeparateDifferences()
        {
            var synchronizedState = TextDiffHighlighter.BuildSynchronizedState(
                "global persist $top = 4\nglobal persist $collar = 1",
                "global persist $top = 0\nglobal persist $collar = 0",
                "global persist $top = 4\nglobal persist $collar = 1");

            Assert.Equal(2, synchronizedState.TotalDifferenceCount);
            Assert.Equal(1, synchronizedState.ResultEditor.DifferenceOrdinalByLineIndex[0]);
            Assert.Equal(2, synchronizedState.ResultEditor.DifferenceOrdinalByLineIndex[1]);
        }

        [Fact]
        public void BuildSynchronizedState_ShouldEmitPlaceholderLinesToKeepThreeEditorsAligned()
        {
            var synchronizedState = TextDiffHighlighter.BuildSynchronizedState(
                "same\nold-only\nline-a\nline-b",
                "same\nline-a\nline-b",
                "same\nline-a\nresult-only\nline-b");

            Assert.Equal(synchronizedState.NewEditor.Lines.Count, synchronizedState.OldEditor.Lines.Count);
            Assert.Equal(synchronizedState.NewEditor.Lines.Count, synchronizedState.ResultEditor.Lines.Count);

            Assert.True(synchronizedState.NewEditor.Lines[1].IsPlaceholder);
            Assert.True(synchronizedState.OldEditor.Lines[3].IsPlaceholder);
            Assert.False(synchronizedState.ResultEditor.Lines[3].IsPlaceholder);
        }

        [Fact]
        public void BuildSynchronizedState_ShouldAlignPersistGlobalsWithNonPersistGlobalsAndLeaveCommentPlaceholders()
        {
            var synchronizedState = TextDiffHighlighter.BuildSynchronizedState(
                "global persist $key_1 = 0\nglobal persist $key_2 = 0\nglobal persist $key_3 = 0",
                "; 发饰耳朵\nglobal $key_1 = 0\n; 发饰头带\nglobal $key_2 = 0\nglobal $key_3 = 0",
                "; 发饰耳朵\nglobal $key_1 = 0\n; 发饰头带\nglobal $key_2 = 0\nglobal $key_3 = 0");

            Assert.Equal(synchronizedState.NewEditor.Lines.Count, synchronizedState.OldEditor.Lines.Count);
            Assert.Equal(5, synchronizedState.OldEditor.Lines.Count);

            Assert.True(synchronizedState.OldEditor.Lines[0].IsPlaceholder);
            Assert.Equal("global persist $key_1 = 0", synchronizedState.OldEditor.Lines[1].Text);
            Assert.True(synchronizedState.OldEditor.Lines[2].IsPlaceholder);
            Assert.Equal("global persist $key_2 = 0", synchronizedState.OldEditor.Lines[3].Text);
            Assert.Equal("global persist $key_3 = 0", synchronizedState.OldEditor.Lines[4].Text);

            Assert.Equal("global $key_1 = 0", synchronizedState.NewEditor.Lines[1].Text);
            Assert.Equal("global $key_2 = 0", synchronizedState.NewEditor.Lines[3].Text);
            Assert.Equal("global $key_3 = 0", synchronizedState.NewEditor.Lines[4].Text);
        }

        [Fact]
        public void BuildSynchronizedState_ShouldAlignDrawComponentCommentAndDrawIndexedLinesWithoutPlaceholders()
        {
            var synchronizedState = TextDiffHighlighter.BuildSynchronizedState(
                "if $key_11 == 1\n    ; Draw Component 3.10发带\n    drawindexed = 546, 288522, 0\nendif",
                "if $key_11 == 1\n    ; Draw Component 3.10春联-胸饰\n    drawindexed = 10632, 56382, 0\nendif",
                "if $key_11 == 1\n    ; Draw Component 3.10春联-胸饰\n    drawindexed = 10632, 56382, 0\nendif");

            Assert.Equal(4, synchronizedState.OldEditor.Lines.Count);
            Assert.Equal(4, synchronizedState.NewEditor.Lines.Count);
            Assert.Equal(4, synchronizedState.ResultEditor.Lines.Count);

            Assert.False(synchronizedState.OldEditor.Lines[1].IsPlaceholder);
            Assert.False(synchronizedState.OldEditor.Lines[2].IsPlaceholder);
            Assert.False(synchronizedState.NewEditor.Lines[1].IsPlaceholder);
            Assert.False(synchronizedState.NewEditor.Lines[2].IsPlaceholder);

            Assert.Equal("    ; Draw Component 3.10发带", synchronizedState.OldEditor.Lines[1].Text);
            Assert.Equal("    ; Draw Component 3.10春联-胸饰", synchronizedState.NewEditor.Lines[1].Text);
            Assert.Equal("    drawindexed = 546, 288522, 0", synchronizedState.OldEditor.Lines[2].Text);
            Assert.Equal("    drawindexed = 10632, 56382, 0", synchronizedState.NewEditor.Lines[2].Text);

            Assert.Equal(1, synchronizedState.TotalDifferenceCount);
            Assert.Equal(1, synchronizedState.ResultEditor.DifferenceOrdinalByLineIndex[1]);
            Assert.Equal(1, synchronizedState.ResultEditor.DifferenceOrdinalByLineIndex[2]);
        }

        [Fact]
        public void BuildSynchronizedState_ShouldKeepDrawComponentAndDrawIndexedAlignedWhenNewAddsTrailingDrawBlock()
        {
            var synchronizedState = TextDiffHighlighter.BuildSynchronizedState(
                "if $key_10 == 1\n    ; Draw Component 3.9春联-内裤\n    drawindexed = 1038, 287484, 0\nendif\nif $key_11 == 1\n    ; Draw Component 3.10发带\n    drawindexed = 546, 288522, 0\nendif\nif $key_12 == 1\n    ; Draw Component 3.11流苏\n    drawindexed = 1836, 56382, 0\nendif",
                "if $key_10 == 1\n    ; Draw Component 3.9春联-内裤\n    drawindexed = 1038, 298116, 0\nendif\nif $key_11 == 1\n    ; Draw Component 3.10春联-胸饰\n    drawindexed = 10632, 56382, 0\nendif\nif $key_12 == 1\n    ; Draw Component 3.11流苏\n    drawindexed = 1836, 67014, 0\nendif\n; Draw Component 3.发带\ndrawindexed = 546, 299154, 0",
                "if $key_10 == 1\n    ; Draw Component 3.9春联-内裤\n    drawindexed = 1038, 298116, 0\nendif\nif $key_11 == 1\n    ; Draw Component 3.10春联-胸饰\n    drawindexed = 10632, 56382, 0\nendif\nif $key_12 == 1\n    ; Draw Component 3.11流苏\n    drawindexed = 1836, 67014, 0\nendif\n; Draw Component 3.发带\ndrawindexed = 546, 299154, 0");

            Assert.Equal("    ; Draw Component 3.10发带", synchronizedState.OldEditor.Lines[5].Text);
            Assert.Equal("    ; Draw Component 3.10春联-胸饰", synchronizedState.NewEditor.Lines[5].Text);
            Assert.Equal("    drawindexed = 546, 288522, 0", synchronizedState.OldEditor.Lines[6].Text);
            Assert.Equal("    drawindexed = 10632, 56382, 0", synchronizedState.NewEditor.Lines[6].Text);
            Assert.False(synchronizedState.OldEditor.Lines[5].IsPlaceholder);
            Assert.False(synchronizedState.OldEditor.Lines[6].IsPlaceholder);

            Assert.True(synchronizedState.OldEditor.Lines[^2].IsPlaceholder);
            Assert.True(synchronizedState.OldEditor.Lines[^1].IsPlaceholder);
            Assert.Equal("; Draw Component 3.发带", synchronizedState.NewEditor.Lines[^2].Text);
            Assert.Equal("drawindexed = 546, 299154, 0", synchronizedState.NewEditor.Lines[^1].Text);
        }

        [Fact]
        public void BuildSourceLineTextByReferenceLineIndexForReplacement_ShouldPreferCurrentLineReplacementBeforeAdjacentInsertions()
        {
            var lineMap = TextDiffHighlighter.BuildSourceLineTextByReferenceLineIndexForReplacement(
                "same\nold-a\nsame2",
                "same\nresult-a\ninserted\nsame2");

            Assert.Equal("same", lineMap[0]);
            Assert.Equal("old-a", lineMap[1]);
            Assert.Null(lineMap[2]);
            Assert.Equal("same2", lineMap[3]);
        }

        [Fact]
        public void FindNavigationTargetOrdinal_ShouldWrapAtEdges()
        {
            var indices = new[] { 1, 3, 6 };
            var ordinals = new Dictionary<int, int>
            {
                [1] = 1,
                [3] = 2,
                [6] = 3
            };

            Assert.Equal(1, TextDiffHighlighter.FindNavigationTargetOrdinal(indices, ordinals, 6, moveNext: true, totalDifferenceCount: 3));
            Assert.Equal(3, TextDiffHighlighter.FindNavigationTargetOrdinal(indices, ordinals, 1, moveNext: false, totalDifferenceCount: 3));
        }

        [Fact]
        public void FindNavigationTargetOrdinal_ShouldUseNearestVisibleDifferenceWhenCaretIsNotOnDifference()
        {
            var indices = new[] { 1, 3, 6 };
            var ordinals = new Dictionary<int, int>
            {
                [1] = 1,
                [3] = 2,
                [6] = 3
            };

            Assert.Equal(2, TextDiffHighlighter.FindNavigationTargetOrdinal(indices, ordinals, 2, moveNext: true, totalDifferenceCount: 3));
            Assert.Equal(1, TextDiffHighlighter.FindNavigationTargetOrdinal(indices, ordinals, 2, moveNext: false, totalDifferenceCount: 3));
        }

        [Fact]
        public void FindLineIndexByDifferenceOrdinal_ShouldReturnMappedLineIndex()
        {
            var ordinals = new Dictionary<int, int>
            {
                [4] = 1,
                [9] = 2,
                [12] = 3
            };

            Assert.Equal(9, TextDiffHighlighter.FindLineIndexByDifferenceOrdinal(ordinals, 2));
            Assert.Null(TextDiffHighlighter.FindLineIndexByDifferenceOrdinal(ordinals, 4));
        }
    }
}