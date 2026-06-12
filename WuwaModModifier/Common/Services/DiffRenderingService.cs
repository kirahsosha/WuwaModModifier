using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using WuwaModModifier.Model;

namespace WuwaModModifier.Common
{
    /// <summary>
    /// Builds WPF FlowDocuments for the version-sync diff comparison editors.
    /// Extracted from VersionSyncWindow.xaml.cs to keep code-behind thin.
    /// </summary>
    public static class DiffRenderingService
    {
        public static readonly Brush OldDiffBrush = CreateFrozenBrush(Color.FromRgb(0xF8, 0xE1, 0xD6));
        public static readonly Brush NewDiffBrush = CreateFrozenBrush(Color.FromRgb(0xD8, 0xEF, 0xEA));
        public static readonly Brush ResultDiffBrush = CreateFrozenBrush(Color.FromRgb(0xF5, 0xE7, 0xB5));
        public static readonly Brush OldDiffLineBrush = CreateFrozenBrush(Color.FromArgb(0x70, 0xF8, 0xE1, 0xD6));
        public static readonly Brush NewDiffLineBrush = CreateFrozenBrush(Color.FromArgb(0x70, 0xD8, 0xEF, 0xEA));
        public static readonly Brush ResultDiffLineBrush = CreateFrozenBrush(Color.FromArgb(0x70, 0xF5, 0xE7, 0xB5));
        public static readonly Brush PlaceholderLineBrush = CreateFrozenBrush(Color.FromRgb(0xE6, 0xE6, 0xE6));
        public static readonly Brush TransparentBrush = CreateFrozenBrush(Colors.Transparent);

        /// <summary>
        /// Picks the line-highlight brush that pairs with the given inline-diff brush.
        /// </summary>
        public static Brush GetLineDiffBrushForInlineBrush(Brush diffBrush)
        {
            if (ReferenceEquals(diffBrush, OldDiffBrush)) return OldDiffLineBrush;
            if (ReferenceEquals(diffBrush, NewDiffBrush)) return NewDiffLineBrush;
            return ResultDiffLineBrush;
        }

        public static FlowDocument BuildDiffDocument(IReadOnlyList<TextDiffHighlightLine> lines, Brush diffBrush, Action<Paragraph>? setPlaceholder = null)
        {
            var lineDiffBrush = GetLineDiffBrushForInlineBrush(diffBrush);

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
                document.Blocks.Add(BuildParagraph(line, diffBrush, lineDiffBrush, setPlaceholder));
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

        public static Paragraph BuildParagraph(TextDiffHighlightLine line, Brush diffBrush, Brush lineDiffBrush, Action<Paragraph>? setPlaceholder = null)
        {
            var paragraph = new Paragraph
            {
                Margin = new Thickness(0),
                Padding = new Thickness(0)
            };

            if (line.IsPlaceholder)
            {
                paragraph.Background = PlaceholderLineBrush;
                setPlaceholder?.Invoke(paragraph);
                paragraph.Inlines.Add(new Run("\u00A0")
                {
                    Foreground = TransparentBrush,
                    Background = TransparentBrush
                });
                return paragraph;
            }

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

        public static string ReadEditorText(RichTextBox editor, Func<DependencyObject, bool> isPlaceholderParagraph)
        {
            var lines = new List<string>();

            for (Block? block = editor.Document.Blocks.FirstBlock; block != null; block = block.NextBlock)
            {
                if (block is not Paragraph paragraph || isPlaceholderParagraph(paragraph))
                {
                    continue;
                }

                var text = new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text;
                lines.Add(TrimParagraphTerminator(text));
            }

            return string.Join(Environment.NewLine, lines);
        }

        public static Brush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static string TrimParagraphTerminator(string text)
        {
            if (text.EndsWith("\r\n", StringComparison.Ordinal))
            {
                return text[..^2];
            }

            if (text.EndsWith("\n", StringComparison.Ordinal) || text.EndsWith("\r", StringComparison.Ordinal))
            {
                return text[..^1];
            }

            return text;
        }
    }
}
